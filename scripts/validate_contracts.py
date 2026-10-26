"""Validate design artifacts offline; this does not execute the game backend."""
from __future__ import annotations

import argparse
import copy
import json
import re
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any

from jsonschema import Draft202012Validator, FormatChecker
from openapi_spec_validator import validate as validate_openapi

ROOT = Path(__file__).resolve().parents[1]
HTTP_METHODS = {"get", "post", "put", "patch", "delete", "head", "options", "trace"}


def read_json(path: str) -> Any:
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


def oas_refs(value: Any) -> Any:
    if isinstance(value, dict):
        return {
            key: item.replace("#/$defs/", "#/components/schemas/")
            if key == "$ref" else oas_refs(item)
            for key, item in value.items()
        }
    if isinstance(value, list):
        return [oas_refs(item) for item in value]
    return value


def schema_errors(name: str, value: Any, schemas: dict | None = None) -> list:
    schemas = schemas if schemas is not None else read_json("contracts/schemas.json")
    if name not in schemas["$defs"]:
        raise ValueError(f"Unknown schema {name}")
    validator = Draft202012Validator(
        {"$defs": schemas["$defs"], "$ref": f"#/$defs/{name}"},
        format_checker=FormatChecker(),
    )
    return list(validator.iter_errors(value))


def timestamp(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def semantic_errors(value: Any) -> list[str]:
    """Cross-field constraints JSON Schema cannot express with portable keywords."""
    errors: list[str] = []

    def require(ok: bool, message: str) -> None:
        if not ok:
            errors.append(message)

    def visit(item: Any) -> None:
        if isinstance(item, list):
            for child in item:
                visit(child)
            return
        if not isinstance(item, dict):
            return
        if {"board", "sideToMove", "moveNumber", "clock"} <= item.keys():
            board = item["board"]
            require(len({p["id"] for p in board}) == len(board), "duplicate piece ID")
            require(len({(p["position"]["col"], p["position"]["row"]) for p in board}) == len(board), "two pieces share a position")
            for side in ("red", "black"):
                require(sum(p["type"] == "general" and p["side"] == side for p in board) == 1, "one general per side required")
                require(sum(p["side"] == side for p in board) <= 16, "more than 16 pieces per side")
            for piece in board:
                require(piece["id"].startswith(f'{piece["side"]}-{piece["type"]}-'), "piece ID disagrees with side/type")
            require(item["players"]["red"]["id"] != item["players"]["black"]["id"], "player occupies both sides")
            require(item["sideToMove"] == ("red" if item["moveNumber"] % 2 == 0 else "black"), "turn disagrees with ply parity")
            for side in ("red", "black"):
                ledger = item["recovery"][side]
                if ledger["disconnectedAt"] is not None:
                    require(timestamp(ledger["deadlineAt"]) == timestamp(ledger["disconnectedAt"]) + timedelta(milliseconds=ledger["remainingMs"]), "reconnect deadline disagrees with remaining budget")
            offer = item["pendingDraw"]
            if offer is not None:
                require(timestamp(offer["expiresAt"]) - timestamp(offer["createdAt"]) == timedelta(seconds=30), "draw offer must last 30 seconds")
            if item["endedAt"] is not None:
                require(timestamp(item["endedAt"]) >= timestamp(item["startedAt"]), "match ends before it starts")
        if {"hostUserId", "players", "currentMatchId", "timeControl"} <= item.keys():
            players = item["players"]
            require(len({p["side"] for p in players}) == len(players), "duplicate room side")
            require(len({p["user"]["id"] for p in players}) == len(players), "duplicate room player")
            if item["status"] in ("waiting", "playing"):
                require(any(p["side"] == "red" and p["user"]["id"] == item["hostUserId"] for p in players), "host must occupy red seat")
            if item["status"] == "playing":
                require(len(players) == 2, "active match must retain both room players")
        if {"move", "match"} <= item.keys() and item["move"] is not None:
            move, match = item["move"], item["match"]
            require(move["matchId"] == match["id"], "move/match ID mismatch")
            require(move["matchVersion"] == match["version"], "move/match version mismatch")
            require(move["ply"] == match["moveNumber"], "move/match ply mismatch")
        if {"room", "match"} <= item.keys():
            room, match = item["room"], item["match"]
            require(room["currentMatchId"] == match["id"] and room["id"] == match["roomId"], "start room/match mismatch")
            require(room["status"] == "playing" and match["status"] == "playing", "start response must be playing")
            for player in room["players"]:
                require(player["user"]["id"] == match["players"][player["side"]]["id"], "start player/side mismatch")
        if {"eventId", "aggregateId", "version", "data"} <= item.keys():
            data = item["data"]
            if item["type"] in ("RoomUpdated", "MatchUpdated"):
                aggregate = data["room"] if item["type"] == "RoomUpdated" else data["match"]
                require(item["aggregateId"] == aggregate["id"], "event aggregate ID mismatch")
                require(item["version"] == aggregate["version"], "event version mismatch")
                if item["type"] == "MatchUpdated" and data["move"] is not None:
                    require(item["causationId"] == data["move"]["requestId"], "move event causation mismatch")
            else:
                require(item["aggregateId"] == data["roomId"], "chat room mismatch")
                require(item["version"] == data["sequence"], "chat sequence mismatch")
                require(item["causationId"] == data["requestId"], "chat event causation mismatch")
        if "requestId" in item and isinstance(item.get("data"), dict):
            data = item["data"]
            if "move" in data and data["move"] is not None:
                require(item["requestId"] == data["move"]["requestId"], "move response requestId mismatch")
            if {"roomId", "sequence", "requestId"} <= data.keys():
                require(item["requestId"] == data["requestId"], "chat response requestId mismatch")
        for child in item.values():
            visit(child)

    visit(value)
    return errors


def validate_refs(value: Any, document: dict) -> None:
    if isinstance(value, dict):
        if "$ref" in value:
            reference = value["$ref"]
            if not reference.startswith("#/"):
                raise ValueError(f"External reference is not allowed here: {reference}")
            target = document
            for key in reference[2:].split("/"):
                target = target[key.replace("~1", "/").replace("~0", "~")]
        for child in value.values():
            validate_refs(child, document)
    elif isinstance(value, list):
        for child in value:
            validate_refs(child, document)


def validate_artifacts(write_openapi: bool = False) -> dict:
    schemas = read_json("contracts/schemas.json")
    openapi = read_json("contracts/openapi.json")
    hub = read_json("contracts/signalr.json")
    examples = read_json("contracts/examples.json")
    for definition in schemas["$defs"].values():
        Draft202012Validator.check_schema(definition)
    validate_refs(schemas, schemas)
    expected = {name: oas_refs(definition) for name, definition in schemas["$defs"].items()}
    if write_openapi:
        openapi["components"]["schemas"] = expected
        (ROOT / "contracts/openapi.json").write_text(json.dumps(openapi, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if openapi["components"]["schemas"] != expected:
        raise ValueError("OpenAPI schemas are stale; run --write-openapi then review the diff")
    validate_refs(openapi, openapi)
    validate_openapi(openapi)
    operation_ids = []
    for path, methods in openapi["paths"].items():
        for method, operation in methods.items():
            if method not in HTTP_METHODS:
                continue
            operation_ids.append(operation["operationId"])
            supplied = {p["name"] for p in operation.get("parameters", []) if p["in"] == "path" and p.get("required")}
            if supplied != set(re.findall(r"{([^}]+)}", path)):
                raise ValueError(f"Path parameter mismatch: {method} {path}")
            if method == "post":
                request = operation["requestBody"]["content"]["application/json"]["schema"]["$ref"].split("/")[-1]
                if "requestId" not in schemas["$defs"][request]["required"]:
                    raise ValueError(f"POST without requestId: {path}")
    if len(operation_ids) != len(set(operation_ids)):
        raise ValueError("Duplicate operationId")
    for category, key_names in (("methods", ("requestSchema", "successSchema", "errorSchema")), ("events", ("schema",))):
        names = [entry["name"] for entry in hub[category]]
        if len(names) != len(set(names)):
            raise ValueError(f"Duplicate hub {category}")
        for entry in hub[category]:
            for key in key_names:
                if entry[key] not in schemas["$defs"]:
                    raise ValueError(f"Unknown hub schema: {entry[key]}")
    if openapi["info"]["version"] != hub["contractVersion"] or hub["contractVersion"] != examples["contractVersion"]:
        raise ValueError("Contract versions disagree")
    names = [case["name"] for case in examples["cases"]]
    if len(names) != len(set(names)):
        raise ValueError("Duplicate fixture name")
    for case in examples["cases"]:
        errors = schema_errors(case["schema"], case["value"], schemas)
        if bool(errors) == case["valid"]:
            details = errors[0].message if errors else "negative example was accepted"
            raise ValueError(f'{case["name"]}: {details}')
        if case["valid"]:
            errors = semantic_errors(case["value"])
            if errors:
                raise ValueError(f'{case["name"]}: {errors}')
    return {"schemas": len(schemas["$defs"]), "httpOperations": len(operation_ids), "hubMethods": len(hub["methods"]), "events": len(hub["events"]), "validFixtures": sum(c["valid"] for c in examples["cases"]), "invalidFixtures": sum(not c["valid"] for c in examples["cases"])}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write-openapi", action="store_true", help="Regenerate embedded components from schemas.json before checking")
    args = parser.parse_args()
    print(json.dumps(validate_artifacts(args.write_openapi), ensure_ascii=False))
