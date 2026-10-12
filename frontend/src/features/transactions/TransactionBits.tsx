import { Badge } from "@/components/ui/badge"
import { formatCurrency } from "@/lib/format"
import { cn } from "@/lib/utils"
import { type Transaction, type TransactionType, transactionTypeLabels } from "./api"

export function TypeBadge({ type }: { type: TransactionType }) {
  return (
    <Badge variant="secondary" className={type === "Income" ? "bg-success-soft text-success" : "bg-destructive-soft text-destructive"}>
      {transactionTypeLabels[type]}
    </Badge>
  )
}

export function Amount({ transaction }: { transaction: Transaction }) {
  const income = transaction.type === "Income"
  return (
    <span className={cn("tabular font-medium whitespace-nowrap", income ? "text-success" : "text-foreground")}>
      {income ? "+" : "−"} {formatCurrency(transaction.amount)}
    </span>
  )
}
