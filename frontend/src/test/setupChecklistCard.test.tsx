import { render } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { SetupChecklistCard } from '@/features/admin/dashboard/SetupChecklistCard'
import * as blocks from '@/features/admin/dashboard/useDashboardBlocks'
import type { SetupChecklist } from '@/lib/dashboardTypes'

vi.mock('@/features/admin/dashboard/useDashboardBlocks')

const checklist = (over: Partial<SetupChecklist>): SetupChecklist =>
  ({ dismissed: false, complete: false, doneCount: 1, total: 6, items: [], ...over })

function renderCard(data: SetupChecklist | undefined) {
  vi.mocked(blocks.useSetupChecklist).mockReturnValue({ data } as ReturnType<typeof blocks.useSetupChecklist>)
  vi.mocked(blocks.useDismissSetup).mockReturnValue({ mutate: vi.fn(), isPending: false } as unknown as ReturnType<typeof blocks.useDismissSetup>)
  return render(<MemoryRouter><SetupChecklistCard /></MemoryRouter>)
}

describe('SetupChecklistCard', () => {
  it('renders nothing while loading, when complete, or when dismissed', () => {
    expect(renderCard(undefined).container).toBeEmptyDOMElement()
    expect(renderCard(checklist({ complete: true, doneCount: 6 })).container).toBeEmptyDOMElement()
    expect(renderCard(checklist({ dismissed: true })).container).toBeEmptyDOMElement()
  })
  it('renders the card while setup is open', () => {
    expect(renderCard(checklist({})).container).not.toBeEmptyDOMElement()
  })
})
