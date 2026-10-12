# Importação de dados (CSV e XLSX)

Como o Datavello lê planilhas, valida as linhas, detecta duplicidades e grava o resultado.

## Fluxo

| Etapa | Endpoint | O que acontece |
| --- | --- | --- |
| 1. Envio | `POST /api/imports` (multipart, campo `file`) | Valida extensão, tamanho e assinatura do arquivo; lê a primeira planilha; guarda as células como texto (staging) e devolve cabeçalhos, prévia e sugestão de mapeamento. |
| 2. Validação | `POST /api/imports/{id}/validate` | Aplica mapeamento e opções a **todas** as linhas e devolve quantas são válidas, inválidas, duplicadas e vazias, com os erros por linha. Não grava nada. |
| 3. Confirmação | `POST /api/imports/{id}/commit` | Revalida no servidor e grava as linhas válidas em **uma única transação de banco**. |
| Histórico | `GET /api/imports`, `GET /api/imports/{id}` | Lista as importações da empresa e os erros de cada uma. |
| Cancelar | `DELETE /api/imports/{id}` | Remove uma importação ainda pendente (não confirmada). |

O arquivo original **não é armazenado**: apenas o texto das células fica em `ImportJob.StagingData` até a confirmação, quando é apagado. Importações pendentes há mais de 24 horas são removidas automaticamente no próximo envio da empresa.

## Arquivos aceitos e limites

| Regra | Valor padrão | Configuração |
| --- | --- | --- |
| Extensões | `.csv`, `.xlsx` | — (`.xls`, `.xlsm` e outros são recusados) |
| Tamanho máximo | 5 MB | `Import__MaxFileSizeBytes` |
| Linhas de dados | 5.000 | `Import__MaxRows` |
| Linhas na prévia | 20 | `Import__PreviewRows` |

- **CSV:** codificação UTF-8 (com ou sem BOM); se o arquivo não for UTF-8 válido, é lido como Windows-1252 (padrão do Excel em português). O separador (`;`, `,` ou tabulação) é detectado pela primeira linha. Campos entre aspas são suportados.
- **XLSX:** é lida a primeira planilha com dados. Macros e conteúdo ativo nunca são executados: só os valores das células são lidos, e fórmulas usam o último valor calculado salvo no arquivo (não são recalculadas). Datas formatadas como data no Excel são lidas como data; números são lidos como número (o separador decimal escolhido só se aplica a valores digitados como texto).
- A primeira linha é sempre o **cabeçalho**. Os números de linha mostrados nos erros seguem a planilha (cabeçalho = linha 1).
- Arquivos corrompidos, vazios, sem cabeçalho ou acima dos limites geram erro 400 com mensagem explicativa.

## Mapeamento de colunas

Campos do Datavello: **Data**, **Descrição** e **Valor** (obrigatórios), **Tipo** e **Categoria** (opcionais conforme as opções).

Cabeçalhos reconhecidos automaticamente (sem diferenciar maiúsculas, acentos ou espaços):

| Campo | Cabeçalhos |
| --- | --- |
| Data | data, date, dt, data lançamento, data do lançamento, data da transação, data movimento, data pagamento |
| Descrição | descrição, descricao, histórico, description, detalhe, detalhes, lançamento, memo |
| Valor | valor, value, amount, valor (r$), valor r$, total, quantia, montante |
| Tipo | tipo, type, natureza, operação, entrada/saída, e/s, crédito/débito, d/c |
| Categoria | categoria, category, classificação, grupo, plano de contas |

A sugestão é sempre exibida para conferência; o usuário pode alterar qualquer coluna.

## Opções de leitura

- **Formato de data:** `DD/MM/AAAA` (padrão), `MM/DD/AAAA` ou `AAAA-MM-DD`. Anos com 2 dígitos são aceitos; horário após a data é ignorado. Datas ISO (`2026-03-15`) são sempre aceitas.
- **Separador decimal:** vírgula (`1.234,56`, padrão) ou ponto (`1,234.56`). `R$`, espaços e parênteses contábeis `(123,45)` = negativo são tratados.
- **Convenção do tipo:**
  - *Coluna de tipo* — receita: `receita`, `entrada`, `crédito`, `C`, `+`, `income`…; despesa: `despesa`, `saída`, `débito`, `D`, `-`, `expense`, `pagamento`…. O sinal do valor é ignorado (usa-se o valor absoluto).
  - *Sinal do valor* — negativo = despesa, positivo = receita (comum em extratos bancários). Valor zero é erro.
  - *Todas receitas* ou *Todas despesas* — tipo fixo para o arquivo inteiro.
- **Categoria:** o nome é comparado com as categorias da empresa sem diferenciar maiúsculas e acentos. Quando a célula está vazia ou a coluna não foi mapeada, usa-se a **categoria padrão** escolhida para receitas/despesas. Opcionalmente, categorias inexistentes podem ser **criadas automaticamente** (com o tipo das linhas que as usam).

## Validação por linha

Uma linha é rejeitada se tiver qualquer um destes problemas (todos são listados, com coluna e valor original):

- data ausente, inválida ou fora de 1900–2100;
- valor ausente, inválido, zero, com mais de 2 casas decimais ou acima do limite;
- tipo desconhecido (modo coluna) ou valor zero (modo sinal);
- descrição ausente ou com mais de 200 caracteres;
- categoria não encontrada (sem criação automática), não informada sem categoria padrão, ou que não aceita o tipo da linha;
- duplicidade (ver abaixo), quando a opção de ignorar duplicadas está ativa.

**Linhas totalmente vazias** são ignoradas e contadas à parte (não são erro nem são importadas).

## Detecção de duplicidades

Uma linha é considerada **possível duplicidade** quando já existe uma transação com **a mesma data, o mesmo tipo, o mesmo valor e a mesma descrição normalizada** (minúsculas, sem acentos, espaços colapsados):

1. entre as transações já cadastradas **na mesma empresa** (de qualquer origem), ou
2. em uma linha anterior **do mesmo arquivo**.

Por padrão essas linhas **não são importadas** e aparecem como rejeitadas com o motivo "Possível duplicidade". Como vendas idênticas no mesmo dia podem ser legítimas, o usuário pode desmarcar a opção e importá-las mesmo assim. A detecção **nunca altera nem exclui** registros existentes.

## Estratégia transacional

- A validação é repetida no servidor na confirmação (a resposta da etapa 2 não é confiável por si só).
- Categorias criadas automaticamente, transações válidas, erros por linha e o status final são gravados em **uma única transação**: ou tudo é salvo, ou nada é.
- A importação é bloqueada (`SELECT … FOR UPDATE`) durante a confirmação; confirmar duas vezes retorna 409.
- Resultado:
  - `Completed` — todas as linhas não vazias foram importadas;
  - `CompletedWithErrors` — parte importada, parte rejeitada (o resultado mostra exatamente quantas e quais);
  - `Failed` — nenhuma linha válida; nada é gravado além do histórico.
- Transações importadas guardam o `ImportJobId` e aparecem com o selo "Importada".

## Segurança

- Tudo acontece no contexto da empresa ativa: uma importação de outra empresa responde 404 em qualquer etapa, e categorias são resolvidas apenas entre as da empresa.
- O conteúdo das planilhas não é registrado em logs.
- Limites de tamanho e de linhas protegem contra arquivos muito grandes. Um XLSX é um arquivo compactado; o limite de 5 MB do arquivo compactado e a contagem de linhas antes da leitura das células reduzem o risco de arquivos que se expandem demais, mas o limite deve ser mantido baixo em produção.
