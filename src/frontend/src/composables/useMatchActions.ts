import { gameApi, type Match } from '../api/game'

type ActionsApi = Pick<typeof gameApi, 'surrender' | 'offerDraw' | 'acceptDraw' | 'rejectDraw'>
export function createMatchActions(id: string, snapshot: () => Match | null, side: () => string,
  confirmSurrender: () => boolean, api: ActionsApi = gameApi) {
  function playing() { return snapshot()?.status === 'playing' && !snapshot()?.result }
  return {
    async surrender() {
      if (!playing() || !confirmSurrender()) return null
      return api.surrender(id, true)
    },
    async offerDraw() {
      const match = snapshot()
      if (!playing() || match?.mode === 'pve' || match?.mode === 'ai' || match?.drawOfferId) return null
      return api.offerDraw(id)
    },
    async respondDraw(offerId: string, accept: boolean) {
      const match = snapshot()
      if (!playing() || !offerId || match?.drawOfferId !== offerId || match.drawOfferedBy === side()) return null
      return accept ? api.acceptDraw(id, offerId) : api.rejectDraw(id, offerId)
    }
  }
}
