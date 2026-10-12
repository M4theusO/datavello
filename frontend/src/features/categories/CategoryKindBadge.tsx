import { Badge } from "@/components/ui/badge"
import { type CategoryKind, categoryKindLabels } from "./api"

const kindBadge: Record<CategoryKind, string> = {
  Income: "bg-success-soft text-success",
  Expense: "bg-destructive-soft text-destructive",
  Both: "bg-brand-soft text-primary",
}

export function CategoryKindBadge({ kind }: { kind: CategoryKind }) {
  return (
    <Badge variant="secondary" className={kindBadge[kind]}>
      {categoryKindLabels[kind]}
    </Badge>
  )
}
