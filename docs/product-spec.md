# Projeto Datavello — SaaS de BI e Dashboards

> Especificação original do produto. Fonte de verdade para escopo do MVP.
> Resumo operacional em `/CLAUDE.md`.

## 1. Contexto e objetivo

Quero desenvolver um produto SaaS chamado **Datavello**, uma plataforma web de Business Intelligence (BI) e dashboards para pequenas empresas.

**Nome:** Datavello
**Slogan:** Dados claros. Decisões melhores.
**Idioma:** Português brasileiro (pt-BR)
**Moeda padrão:** Real brasileiro (BRL)
**Formato de data:** DD/MM/AAAA

O objetivo é criar uma plataforma que permita a pequenos empresários visualizar e acompanhar os principais indicadores de seus negócios sem precisar dominar Excel, programação ou ferramentas complexas de BI.

Inicialmente, o projeto será desenvolvido para uso pessoal e testes. Depois, pretendo validar o produto com pequenos negócios e evoluí-lo para um SaaS comercial, no qual vários clientes poderão criar suas próprias contas, cadastrar dados, importar planilhas e configurar dashboards.

Quero começar com um MVP funcional, simples, bonito, seguro e fácil de evoluir.

**Importante:** não desenvolva uma aplicação apenas demonstrativa. Quero funcionalidades reais, persistência de dados e uma estrutura que possa evoluir para produção.

---

## 2. Como o produto deve funcionar

O Datavello será uma plataforma multiempresa (multi-tenant).

Cada empresa terá seu próprio ambiente, com usuários, dados, categorias, importações e dashboards isolados.

O usuário poderá:

1. Criar uma conta e acessar a plataforma.
2. Criar ou acessar sua empresa.
3. Cadastrar receitas e despesas por meio de formulários.
4. Importar arquivos Excel e CSV.
5. Visualizar indicadores e gráficos atualizados a partir dos dados cadastrados.
6. Filtrar os dados por período e categoria.
7. Consultar transações em tabelas.
8. Personalizar os dashboards usando componentes prontos.
9. Editar suas informações e configurações básicas.

O produto deve funcionar inicialmente com dados financeiros e operacionais genéricos. A arquitetura deverá permitir a inclusão futura de módulos para vendas, estoque, serviços, metas e outros indicadores empresariais.

Não tente desenvolver todos esses módulos futuros agora. Priorize o MVP descrito neste documento.

---

## 3. Identidade visual e experiência do usuário

Crie uma identidade visual original para o Datavello.

### Direção visual

- Aparência de SaaS profissional, moderno e confiável.
- Interface limpa, elegante e organizada.
- Inspiração em dashboards modernos de produtos B2B.
- Fundo predominantemente claro, com áreas brancas, tons neutros e azul como cor principal.
- Verde para indicadores positivos e vermelho para alertas ou resultados negativos, sem exageros.
- Tipografia legível e hierarquia visual consistente.
- Ícones consistentes.
- Cards com bordas discretas, sombras sutis e espaçamento adequado.
- Gráficos visualmente atraentes, com legendas, tooltips e eixos legíveis.
- Layout responsivo para desktop, tablet e celular.
- Estados de carregamento, mensagens de erro, telas vazias e confirmações de sucesso.

Evite uma interface genérica, poluída ou com excesso de gradientes e efeitos visuais.

### Logotipo

Crie um logotipo original para o Datavello, preferencialmente com um símbolo geométrico relacionado a dados, métricas, crescimento ou visualização de informações.

O logotipo deve funcionar em fundo claro e escuro. Prefira SVG local para garantir nitidez e facilidade de manutenção.

Não copie marcas, logotipos ou interfaces de outros produtos.

### Navegação

Utilize uma sidebar lateral com:

- Logotipo Datavello.
- Dashboard.
- Transações.
- Categorias.
- Importar dados.
- Personalizar dashboard.
- Configurações.
- Perfil do usuário.

A sidebar deve poder ser expandida ou recolhida, com ícones e tooltips quando estiver minimizada.

Inclua uma barra superior com o título da página, seletor de período quando aplicável, notificações visuais quando necessárias e menu do usuário.

---

## 4. Stack tecnológica

Utilize a seguinte stack, salvo se houver uma incompatibilidade técnica real que precise ser explicada antes de uma alteração.

### Frontend

- React.
- TypeScript.
- Vite.
- Tailwind CSS.
- shadcn/ui.
- Recharts para gráficos.
- React Router para navegação.
- TanStack Query para chamadas à API e gerenciamento de cache.
- React Hook Form e Zod para formulários e validações.
- Lucide React para ícones.

### Backend

- C#.
- ASP.NET Core Web API.
- Entity Framework Core.
- PostgreSQL.
- Swagger/OpenAPI.
- Autenticação segura, utilizando ASP.NET Core Identity ou uma solução equivalente bem integrada.

### Ambiente de desenvolvimento

- Docker Compose para executar o PostgreSQL localmente.
- Arquivos `.env` para configurações locais.
- `.env.example` sem segredos reais.
- Migrations do Entity Framework Core.
- README com instruções de instalação, configuração e execução.

Utilize versões estáveis e compatíveis das dependências. Verifique as versões disponíveis no ambiente antes de instalar ou configurar os componentes.

Não armazene senhas, tokens ou credenciais no código-fonte.

---

## 5. Arquitetura e organização

Organize o projeto como um monólito modular, com frontend e backend separados.

Estrutura inicial sugerida:

```text
datavello/
├── frontend/
├── backend/
├── docs/
│   ├── architecture.md
│   ├── implementation-plan.md
│   └── data-import.md
├── docker-compose.yml
├── .env.example
├── .gitignore
└── README.md
```

Adapte a estrutura interna de cada aplicação conforme as boas práticas da stack escolhida.

### Princípios arquiteturais

- Separação clara entre apresentação, regras de negócio e persistência.
- Controllers enxutos.
- Serviços para regras de negócio.
- DTOs para entrada e saída da API.
- Validação dos dados recebidos.
- Tratamento centralizado de erros.
- Paginação para tabelas com muitos registros.
- Consultas eficientes ao PostgreSQL.
- Migrations versionadas.
- Testes automatizados para regras importantes.
- Código legível e fácil de manter.
- Evitar abstrações desnecessárias e complexidade prematura.

Não crie microserviços neste MVP.

---

## 6. Multi-tenancy e isolamento dos dados

A arquitetura deve permitir que várias empresas utilizem a mesma aplicação sem compartilhar seus dados.

Crie entidades compatíveis com esse modelo, incluindo:

- User.
- Tenant ou Company.
- TenantMember, caso a associação entre usuários e empresas seja separada.
- Transaction.
- Category.
- ImportJob.
- ImportRowError.
- DashboardTemplate.
- Dashboard.
- WidgetConfig.

Ajuste os nomes e relacionamentos quando necessário para manter um modelo consistente.

### Regras obrigatórias

- Cada empresa possui um identificador próprio.
- Os dados empresariais devem estar associados à empresa correspondente.
- O backend deve identificar a empresa ativa a partir de uma associação autenticada e validada.
- Nunca confie exclusivamente em um `tenantId` enviado pelo frontend.
- Verifique autorização em todas as operações de leitura, criação, atualização e exclusão.
- Um usuário não pode acessar dados de outra empresa alterando IDs na URL, query string ou corpo da requisição.
- Os testes devem verificar o isolamento entre empresas.
- Não utilize apenas filtros no frontend como mecanismo de segurança.

Comece com usuários individuais e empresas. A possibilidade de vários usuários por empresa deve ser considerada na arquitetura, mas convites, permissões avançadas e gestão complexa de equipes não são obrigatórios no primeiro MVP.

---

## 7. Autenticação e segurança

Implemente:

- Cadastro de usuário.
- Login.
- Logout.
- Senhas armazenadas por mecanismo seguro de hash.
- Rotas protegidas.
- Autorização no backend.
- Validação de entradas.
- Tratamento seguro de erros.
- Proteção contra acesso indevido entre empresas.
- Configuração adequada de CORS.
- Segredos fora do repositório.
- Logs sem senhas, tokens ou dados sensíveis desnecessários.

Não implemente autenticação fictícia em produção nem deixe endpoints administrativos abertos.

Se alguma etapa exigir configuração manual, documente exatamente o que precisa ser configurado.

---

## 8. Dashboard principal

Crie um dashboard financeiro e operacional inicial.

Ele deverá apresentar:

### Cards de indicadores

- Total de receitas no período.
- Total de despesas no período.
- Saldo do período.
- Quantidade de transações.
- Comparação com o período anterior, quando houver dados suficientes.

### Gráficos

- Evolução mensal de receitas e despesas.
- Distribuição de despesas por categoria.
- Comparativo de receitas e despesas.
- Histórico do saldo por período, quando fizer sentido.

### Tabela de atividade recente

Exiba as transações mais recentes, com:

- Data.
- Descrição.
- Categoria.
- Tipo.
- Valor.

### Filtros

- Período predefinido.
- Intervalo de datas personalizado.
- Categoria.
- Tipo de transação.

Todos os cards, gráficos e tabelas devem respeitar os filtros selecionados.

**Importante:** identifique o indicador como “saldo do período” ou equivalente. Não o apresente automaticamente como lucro contábil ou fluxo de caixa completo. Explique, quando necessário, que os resultados dependem dos dados cadastrados.

Os indicadores devem ser calculados a partir do banco de dados, e não de números fixos no frontend.

---

## 9. Cadastro e gerenciamento de transações

Crie uma página de transações com tabela, filtros e ações.

Cada transação deverá possuir, no mínimo:

- ID.
- Empresa associada.
- Data.
- Descrição.
- Tipo: receita ou despesa.
- Valor monetário.
- Categoria.
- Observação opcional.
- Data de criação.
- Data de atualização.

### Funcionalidades

- Cadastrar transação.
- Editar transação.
- Excluir transação com confirmação.
- Pesquisar por descrição.
- Filtrar por período, categoria e tipo.
- Ordenar por data e valor.
- Paginar os resultados.
- Exibir mensagens de sucesso e erro.

Utilize tipos numéricos apropriados para valores monetários no backend e no banco. Evite cálculos financeiros com ponto flutuante binário quando isso puder gerar imprecisão.

---

## 10. Categorias

Crie uma página para gerenciar categorias de receitas e despesas.

Funcionalidades:

- Criar categoria.
- Editar categoria.
- Excluir categoria, respeitando os vínculos existentes.
- Definir nome.
- Definir tipo aplicável.
- Exibir a quantidade de transações associadas, quando útil.

Cada empresa deverá ter suas próprias categorias.

Inclua algumas categorias padrão para facilitar os primeiros testes, sem impedir que o usuário crie categorias próprias.

---

## 11. Importação de Excel e CSV

A importação de planilhas é uma funcionalidade central do Datavello.

O usuário deve conseguir importar dados sem precisar alterar manualmente o código da aplicação.

### Fluxo de importação

1. O usuário acessa a página “Importar dados”.
2. Seleciona um arquivo CSV ou Excel (`.xlsx`).
3. A aplicação verifica o formato e o tamanho do arquivo.
4. O sistema lê as colunas e apresenta uma prévia dos registros.
5. O usuário associa as colunas da planilha aos campos do Datavello.
6. O usuário define, quando necessário, os formatos de data, separador decimal e convenção de valores.
7. O sistema valida os registros.
8. O usuário visualiza quantas linhas são válidas e quantas possuem erros.
9. O usuário confirma a importação.
10. O sistema persiste os registros válidos e apresenta o resultado.

### Campos mínimos para mapear

- Data.
- Descrição.
- Tipo.
- Valor.
- Categoria, quando disponível.

Permita reconhecer cabeçalhos comuns automaticamente, mas sempre mostre o mapeamento para conferência.

### Tratamento de erros

- Data inválida.
- Valor inválido.
- Tipo de transação desconhecido.
- Campos obrigatórios ausentes.
- Categoria não encontrada.
- Linhas vazias.
- Registros duplicados, quando detectáveis.

O sistema deve permitir ao usuário identificar a linha problemática e entender como corrigi-la.

### Histórico de importações

Registre:

- Nome do arquivo.
- Data e hora.
- Usuário responsável.
- Quantidade de linhas analisadas.
- Quantidade importada.
- Quantidade rejeitada.
- Status da importação.
- Mensagens de erro relevantes.

### Cuidados importantes

- Valide o conteúdo no backend, mesmo que exista validação no frontend.
- Limite o tamanho dos arquivos e a quantidade de linhas conforme parâmetros configuráveis.
- Não execute macros ou conteúdo ativo de planilhas.
- Proteja o processamento contra arquivos malformados.
- Não exponha dados de outras empresas durante a importação.
- Evite importar parcialmente sem informar claramente o resultado.
- Defina uma estratégia transacional consistente para confirmar a importação.
- Documente as regras de detecção de duplicidades e evite excluir registros legítimos por engano.

Implemente suporte real a CSV e XLSX, utilizando bibliotecas apropriadas e mantidas.

---

## 12. Personalização do dashboard

O Datavello deverá utilizar componentes prontos, sem exigir que o cliente programe.

Crie uma página de personalização na qual o usuário possa:

- Ativar e desativar widgets.
- Alterar a ordem dos widgets.
- Escolher entre os widgets disponíveis.
- Definir o período padrão.
- Salvar suas preferências.
- Restaurar o layout padrão.

A configuração deve ser salva no banco de dados e associada ao dashboard e à empresa correspondente.

Comece com os widgets financeiros definidos anteriormente.

Não implemente um editor visual complexo de consultas SQL, fórmulas arbitrárias ou gráficos totalmente livres neste MVP.

---

## 13. Dados de demonstração

Quero testar a plataforma sozinho sem precisar cadastrar tudo manualmente.

Crie um mecanismo de seed para ambiente de desenvolvimento que inclua:

- Uma empresa fictícia.
- Um usuário de demonstração com credenciais documentadas apenas para ambiente local.
- Categorias de receitas e despesas.
- Transações distribuídas ao longo de vários meses.
- Dados suficientes para preencher os gráficos e demonstrar filtros.
- Um dashboard inicial configurado.

Os dados devem ser claramente identificados como fictícios.

O seed deve ser seguro para execução repetida e não pode apagar os dados reais do usuário.

Não utilize essas credenciais de demonstração em ambientes de produção.

---

## 14. Telas do MVP

Implemente as seguintes telas:

1. Login.
2. Cadastro.
3. Dashboard.
4. Transações.
5. Criar e editar transação.
6. Categorias.
7. Importar dados.
8. Histórico de importações.
9. Personalizar dashboard.
10. Configurações da empresa.
11. Perfil do usuário.
12. Página de acesso negado ou recurso indisponível, quando necessário.

As páginas devem estar integradas ao backend.

Evite botões meramente decorativos. Toda ação visível deve funcionar ou estar explicitamente identificada como indisponível no MVP.

---

## 15. API e persistência

Crie endpoints REST organizados por recurso, com contratos claros.

A API deve oferecer as operações necessárias para:

- Autenticação.
- Empresa ativa.
- Dashboard e indicadores.
- Transações.
- Categorias.
- Importações.
- Histórico de importações.
- Configuração dos widgets.
- Perfil e configurações.

Use paginação para listagens e filtros por parâmetros documentados.

Os indicadores do dashboard devem ser calculados no backend a partir dos dados persistidos, respeitando o isolamento da empresa e os filtros solicitados.

Documente os endpoints no Swagger e, quando útil, inclua exemplos de requisição e resposta.

---

## 16. Testes e qualidade

Implemente testes proporcionais ao MVP, priorizando regras críticas.

Inclua testes para:

- Cálculo de receitas, despesas e saldo.
- Filtros por período.
- Validação de transações.
- Importação de CSV.
- Importação de XLSX.
- Tratamento de linhas inválidas.
- Detecção de duplicidades, conforme a regra definida.
- Autorização.
- Isolamento de dados entre empresas.
- Persistência das configurações do dashboard.

Execute os testes e corrija os problemas encontrados.

Não declare um teste como aprovado sem executá-lo de fato.

---

## 17. Fora do escopo inicial

Não implemente agora:

- Cobrança recorrente ou integração com gateways de pagamento.
- Planos comerciais automatizados.
- Integrações com ERPs.
- Integrações bancárias.
- Inteligência artificial para análise de dados.
- Aplicativo mobile nativo.
- Editor avançado de dashboards.
- Marketplace de templates.
- Permissões corporativas complexas.
- Múltiplos bancos de dados por cliente.
- Infraestrutura de microserviços.

Prepare uma arquitetura que permita adicionar essas funcionalidades no futuro sem implementá-las prematuramente.

---

## 18. Etapas obrigatórias de execução

Quero que você desenvolva o projeto por etapas, sem tentar criar tudo de uma vez.

### Etapa 1 — Inspeção e planejamento

Antes de modificar arquivos:

- Inspecione o diretório atual e as ferramentas instaladas.
- Verifique as versões do Node.js, npm, .NET e Docker disponíveis.
- Identifique arquivos e projetos existentes.
- Não sobrescreva arquivos sem verificar seu conteúdo.
- Crie `docs/architecture.md`.
- Crie `docs/implementation-plan.md`.
- Registre as principais decisões arquiteturais e os riscos técnicos.

Se o diretório já contiver um projeto, adapte-se a ele em vez de recriar tudo indiscriminadamente.

### Etapa 2 — Fundação

- Configure a estrutura do frontend e backend.
- Configure PostgreSQL e Docker Compose.
- Implemente a conexão com o banco.
- Configure migrations.
- Crie os componentes visuais básicos.
- Implemente autenticação e autorização inicial.
- Crie o modelo multiempresa.

### Etapa 3 — Funcionalidades centrais

- Implemente transações.
- Implemente categorias.
- Implemente os cálculos dos indicadores.
- Crie o dashboard funcional.
- Implemente filtros e paginação.

### Etapa 4 — Importação e personalização

- Implemente CSV e XLSX.
- Crie o mapeamento de colunas.
- Implemente prévia e validação.
- Crie o histórico de importações.
- Implemente personalização e persistência dos widgets.

### Etapa 5 — Testes e acabamento

- Execute os testes automatizados.
- Corrija erros.
- Verifique as telas em diferentes tamanhos de tela.
- Revise segurança e isolamento entre empresas.
- Melhore mensagens de erro e estados vazios.
- Atualize a documentação.

### Etapa 6 — Execução local

Garanta que eu consiga executar o projeto na minha máquina Windows.

Documente comandos exatos para:

- Subir o PostgreSQL.
- Restaurar dependências.
- Aplicar migrations.
- Criar os dados de demonstração.
- Iniciar o backend.
- Iniciar o frontend.
- Executar os testes.

Explique como acessar a aplicação e quais credenciais de demonstração usar em ambiente local.

---

## 19. Regras para trabalhar comigo

- Não tente implementar todas as etapas em uma única alteração gigantesca.
- Trabalhe em etapas pequenas e verificáveis.
- Antes de uma mudança arquitetural relevante, explique a necessidade e as alternativas.
- Não pare para pedir confirmação sobre decisões técnicas simples; escolha a alternativa mais razoável e documente a decisão.
- Se houver um bloqueio real, explique o problema e apresente opções concretas.
- Não invente que funcionalidades, testes ou comandos funcionaram.
- Não deixe dados fictícios hardcoded nos componentes de produção.
- Não esconda erros de API com dados falsos.
- Não adicione dependências sem necessidade.
- Priorize simplicidade, segurança e facilidade de manutenção.
- Preserve a consistência visual entre todas as telas.
- Escreva nomes de classes, variáveis e funções de forma consistente, preferencialmente em inglês; a interface deve estar em português brasileiro.
- Mantenha o projeto preparado para evoluir de uma aplicação de uso pessoal para um SaaS multiempresa.

## 20. Primeira tarefa

Comece pela **Etapa 1 — Inspeção e planejamento**.

Inspecione o ambiente, registre as decisões de arquitetura e apresente um plano de implementação objetivo. Depois, inicie a primeira etapa de desenvolvimento que possa ser concluída e testada com segurança.

Ao final de cada etapa, informe:

1. O que foi implementado.
2. Quais arquivos foram criados ou alterados.
3. Quais comandos foram executados.
4. Quais testes foram executados e seus resultados.
5. Como executar e testar a funcionalidade.
6. Quais limitações ou pendências permanecem.

O resultado esperado é um MVP real do **Datavello**, com uma interface profissional, funcionalidades integradas, banco de dados persistente e uma base segura para testes locais e evolução comercial.
