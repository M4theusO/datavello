import { describe, expect, it } from "vitest"
import { rangeForPreset } from "../periods"

// 12/10/2026 (local time)
const today = new Date(2026, 9, 12)

describe("rangeForPreset", () => {
  it.each([
    ["ThisMonth", "2026-10-01", "2026-10-31"],
    ["LastMonth", "2026-09-01", "2026-09-30"],
    ["Last3Months", "2026-08-01", "2026-10-31"],
    ["Last6Months", "2026-05-01", "2026-10-31"],
    ["Last12Months", "2025-11-01", "2026-10-31"],
    ["ThisYear", "2026-01-01", "2026-12-31"],
    ["Last30Days", "2026-09-13", "2026-10-12"],
  ] as const)("%s", (preset, from, to) => {
    expect(rangeForPreset(preset, today)).toEqual({ from, to })
  })

  it("crosses year boundaries", () => {
    expect(rangeForPreset("LastMonth", new Date(2026, 0, 15))).toEqual({ from: "2025-12-01", to: "2025-12-31" })
  })

  it("has no fixed range for custom and all", () => {
    expect(rangeForPreset("custom", today)).toBeNull()
    expect(rangeForPreset("all", today)).toBeNull()
  })
})
