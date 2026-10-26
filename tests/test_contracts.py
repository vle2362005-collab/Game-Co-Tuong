"""Contract regressions, not backend/rules-engine integration tests."""
import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from validate_contracts import read_json, schema_errors, semantic_errors, validate_artifacts


class ContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = {c["name"]: c for c in read_json("contracts/examples.json")["cases"]}

    def fixture(self, name):
        return copy.deepcopy(self.cases[name]["value"])

    def test_complete_contract_bundle(self):
        result = validate_artifacts()
        self.assertGreater(result["invalidFixtures"], 20)

    def test_event_version_mismatch(self):
        value = self.fixture("match-event")
        value["version"] += 1
        self.assertIn("event version mismatch", semantic_errors(value))

    def test_move_response_has_one_matching_ply_and_version(self):
        for field in ("ply", "matchVersion"):
            with self.subTest(field=field):
                value = self.fixture("move-accepted")
                value["data"]["move"][field] += 1
                self.assertTrue(semantic_errors(value))

    def test_receipt_request_id_matches_move(self):
        value = self.fixture("move-accepted")
        value["requestId"] = "00000000-0000-4000-8000-000000000999"
        self.assertIn("move response requestId mismatch", semantic_errors(value))

    def test_start_cannot_reference_another_match(self):
        value = self.fixture("start-response")
        value["data"]["room"]["currentMatchId"] = "00000000-0000-4000-8000-000000000999"
        self.assertIn("start room/match mismatch", semantic_errors(value))

    def test_chat_uses_its_own_sequence(self):
        value = self.fixture("chat-event")
        value["version"] += 1
        self.assertIn("chat sequence mismatch", semantic_errors(value))

    def test_match_version_can_change_without_a_move(self):
        value = self.fixture("draw-offered")
        self.assertEqual(value["moveNumber"], 0)
        self.assertGreater(value["version"], 1)
        self.assertFalse(schema_errors("MatchSnapshot", value))
        self.assertFalse(semantic_errors(value))

    def test_reconnect_deadline_uses_remaining_not_new_budget(self):
        value = self.fixture("paused-after-20s-used")
        self.assertEqual(value["recovery"]["red"]["remainingMs"], 40000)
        value["recovery"]["red"]["deadlineAt"] = "2026-10-26T07:32:00Z"
        self.assertIn("reconnect deadline disagrees with remaining budget", semantic_errors(value))

    def test_restart_preserves_different_player_deadlines(self):
        value = self.fixture("restart-recovery")
        self.assertNotEqual(value["recovery"]["red"]["deadlineAt"], value["recovery"]["black"]["deadlineAt"])
        self.assertFalse(schema_errors("MatchSnapshot", value))
        self.assertFalse(semantic_errors(value))

    def test_draw_offer_must_last_30_seconds(self):
        value = self.fixture("draw-offered")
        value["pendingDraw"]["expiresAt"] = "2026-10-26T07:32:00Z"
        self.assertIn("draw offer must last 30 seconds", semantic_errors(value))

    def test_duplicate_positions_and_players_are_rejected(self):
        value = self.fixture("initial-board")
        value["board"][1]["position"] = value["board"][0]["position"]
        value["players"]["black"] = value["players"]["red"]
        errors = semantic_errors(value)
        self.assertIn("two pieces share a position", errors)
        self.assertIn("player occupies both sides", errors)

    def test_playing_paused_and_terminal_constraints(self):
        for name in ("reject-paused-running-clock", "reject-paused-with-draw", "reject-playing-recovery", "reject-draw-with-winner", "reject-finished-cancellation", "reject-unpaired-deadline"):
            with self.subTest(name=name):
                case = self.cases[name]
                self.assertTrue(schema_errors(case["schema"], case["value"]))


if __name__ == "__main__":
    unittest.main()
