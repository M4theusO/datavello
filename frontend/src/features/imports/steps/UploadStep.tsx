import { type DragEvent, useRef, useState } from "react"
import { AlertCircle, Download, FileSpreadsheet, UploadCloud } from "lucide-react"
import { Spinner } from "@/components/feedback"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { errorMessage } from "@/lib/api"
import { cn } from "@/lib/utils"
import { type ImportPreview, MAX_FILE_SIZE, useUploadImport } from "../api"

const TEMPLATE =
  "Data;Descrição;Valor;Tipo;Categoria\r\n" +
  "05/01/2026;Venda balcão;1.250,00;Receita;Vendas\r\n" +
  "06/01/2026;Aluguel do ponto;3.200,00;Despesa;Aluguel\r\n" +
  "08/01/2026;Conta de energia;480,35;Despesa;Água, luz e internet\r\n"

function downloadTemplate() {
  // BOM so Excel opens the accents correctly.
  const blob = new Blob(["﻿" + TEMPLATE], { type: "text/csv;charset=utf-8" })
  const url = URL.createObjectURL(blob)
  const link = document.createElement("a")
  link.href = url
  link.download = "modelo-importacao-datavello.csv"
  link.click()
  URL.revokeObjectURL(url)
}

function checkFile(file: File): string | null {
  const name = file.name.toLowerCase()
  if (!name.endsWith(".csv") && !name.endsWith(".xlsx")) {
    return "Formato não suportado. Envie um arquivo .csv ou .xlsx (arquivos .xls e .xlsm não são aceitos)."
  }
  if (file.size === 0) return "O arquivo está vazio."
  if (file.size > MAX_FILE_SIZE) return "O arquivo excede o tamanho máximo de 5 MB."
  return null
}

export function UploadStep({ onUploaded }: { onUploaded: (preview: ImportPreview) => void }) {
  const upload = useUploadImport()
  const input = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const [localError, setLocalError] = useState<string | null>(null)

  async function handleFile(file: File | undefined) {
    if (!file) return
    const problem = checkFile(file)
    setLocalError(problem)
    if (problem) return
    try {
      onUploaded(await upload.mutateAsync(file))
    } catch {
      // Shown below from upload.error.
    } finally {
      if (input.current) input.current.value = ""
    }
  }

  function onDrop(event: DragEvent) {
    event.preventDefault()
    setDragging(false)
    void handleFile(event.dataTransfer.files[0])
  }

  const error = localError ?? (upload.isError ? errorMessage(upload.error) : null)

  return (
    <Card>
      <CardContent className="space-y-5">
        {error && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <div
          onDragOver={(e) => {
            e.preventDefault()
            setDragging(true)
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={onDrop}
          className={cn(
            "flex flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed px-6 py-12 text-center transition-colors",
            dragging ? "border-primary bg-brand-soft" : "border-input bg-muted/30",
          )}
        >
          {upload.isPending ? (
            <>
              <Spinner className="size-8 text-primary" />
              <p className="font-medium">Lendo o arquivo…</p>
            </>
          ) : (
            <>
              <div className="flex size-12 items-center justify-center rounded-full bg-brand-soft text-primary">
                <UploadCloud className="size-6" aria-hidden="true" />
              </div>
              <div className="space-y-1">
                <p className="font-medium">Arraste sua planilha para cá</p>
                <p className="text-sm text-muted-foreground">Arquivos .csv ou .xlsx de até 5 MB e 5.000 linhas</p>
              </div>
              <Button onClick={() => input.current?.click()}>
                <FileSpreadsheet data-icon="inline-start" />
                Escolher arquivo
              </Button>
              <input
                ref={input}
                type="file"
                accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                className="sr-only"
                aria-label="Arquivo da planilha"
                onChange={(e) => void handleFile(e.target.files?.[0])}
              />
            </>
          )}
        </div>

        <div className="flex flex-col gap-3 rounded-lg bg-muted/40 p-4 text-sm sm:flex-row sm:items-center sm:justify-between">
          <div className="space-y-1 text-muted-foreground">
            <p className="font-medium text-foreground">Como preparar a planilha</p>
            <p>A primeira linha deve ter os nomes das colunas. Use uma linha por lançamento, com data, descrição e valor.</p>
            <p>Na próxima etapa você confere como cada coluna será usada, antes de importar qualquer dado.</p>
          </div>
          <Button variant="outline" size="sm" className="shrink-0" onClick={downloadTemplate}>
            <Download data-icon="inline-start" />
            Baixar modelo CSV
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}
