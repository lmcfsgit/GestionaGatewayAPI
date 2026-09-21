# Documentação das Chamadas Upstream do Gestiona

<center>Versão 1.9.0</center>

## Índice

### Comportamento comum

- [URL base](#url-base)
- [Cabeçalho de autenticação](#cabeçalho-de-autenticação)

### Chamadas upstream

- [1. GET `/files`](#1-get-files)
- [2. POST `/uploads`](#2-post-uploads)
- [3. PUT `{upload_location}`](#3-put-upload_location)
- [4. POST `/files/{file_id}/documents-and-folders`](#4-post-filesfile_iddocuments-and-folders)
- [5. POST `/files/{file_id}/documents-and-folders/{folder_or_document_id}`](#5-post-filesfile_iddocuments-and-foldersfolder_or_document_id)
- [6. GET `/files/{file_id}/documents-and-folders`](#6-get-filesfile_iddocuments-and-folders)
- [7. GET `/files/{file_id}/documents-and-folders/{folder_or_document_id}`](#7-get-filesfile_iddocuments-and-foldersfolder_or_document_id)
- [8. GET `/content/small/documentinstances/{document_id}`](#8-get-contentsmalldocumentinstancesdocument_id)
- [9. GET `/files/{file_id}/thirdparties`](#9-get-filesfile_idthirdparties)
- [10. GET `/thirds`](#10-get-thirds)
- [11. GET `/thirds/{third_id}`](#11-get-thirdsthird_id)
- [12. GET `/thirds/{third_id}/default-address`](#12-get-thirdsthird_iddefault-address)
- [13. POST `/catalog-2015/procedures/{activity_id}/external-procedures/{procedure_id}/create-file`](#13-post-catalog-2015proceduresactivity_idexternal-proceduresprocedure_idcreate-file)
- [14. GET `/files/{file_id}/selectable-titles`](#14-get-filesfile_idselectable-titles)
- [15. POST `{file_open_href}`](#15-post-file_open_href)
- [16. POST `/files/{file_id}/related-files`](#16-post-filesfile_idrelated-files)
- [17. GET `/files/{file_id}/related-files`](#17-get-filesfile_idrelated-files)
- [18. DELETE `/files/{file_id}/related-files/{related_file_id}`](#18-delete-filesfile_idrelated-filesrelated_file_id)
- [19. GET `/catalog-2015/procedures`](#19-get-catalog-2015procedures)
- [20. GET `/catalog-2015/procedures/{activity_id}/external-procedures`](#20-get-catalog-2015proceduresactivity_idexternal-procedures)
- [21. GET `/files/{file_id}/documents/{document_id}/signatures`](#21-get-filesfile_iddocumentsdocument_idsignatures)
- [22. GET `{signer_user_href}`](#22-get-signer_user_href)
- [23. GET `/files/assignees/users`](#23-get-filesassigneesusers)
- [24. GET `/files/assignees/groups`](#24-get-filesassigneesgroups)
- [25. GET `/connectors`](#25-get-connectors)
- [26. GET `/queues/subscriptions`](#26-get-queuessubscriptions)
- [27. POST `/queues/connectors/{connector_name}/subscription`](#27-post-queuesconnectorsconnector_namesubscription)
- [28. POST `/queues/connectors/{connector_name}?max-messages=100&hold-seconds=1`](#28-post-queuesconnectorsconnector_namemax-messages100hold-seconds1)
- [29. GET `/queues/connectors/{connector_name}/{message_id}`](#29-get-queuesconnectorsconnector_namemessage_id)
- [30. POST `/queues/connectors/{connector_name}/{message_id}`](#30-post-queuesconnectorsconnector_namemessage_id)
- [31. POST `/addon/authorizations`](#31-post-addonauthorizations)
- [32. GET `/addon/authorizations/{auth_id}`](#32-get-addonauthorizationsauth_id)

## Comportamento comum

### URL base

Todas as rotas upstream relativas são enviadas para a URL base configurada da API Gestiona:

- Chave de configuração: `Gestiona:GestionaApiBaseUrl`
- O cliente normaliza a URL base adicionando uma `/` final quando necessário.

### Cabeçalho de autenticação

Todos os pedidos ao Gestiona incluem:

- `X-Gestiona-Access-Token`

O valor do token é resolvido pela camada de serviço do gateway:

1. Utiliza o cabeçalho do pedido de API de entrada `X-User-Access-Token` quando está presente e não está em branco.
2. Caso contrário, utiliza o token configurado em `Gestiona:AccessToken`.

Os endpoints dos conectores de filas são uma exceção ao nível da camada de serviço do gateway: utilizam sempre o token configurado em `Gestiona:AccessToken`.

As chamadas de autorização de add-on utilizam `X-Gestiona-Addon-Token` com o valor configurado em `Gestiona:AddonToken`, em vez de `X-Gestiona-Access-Token`.

## Chamadas upstream

### 1. GET `/files`

Resolve um id de processo Gestiona a partir de um número/código de processo.

#### Utilizado por

- `POST /processes/documents?process_number=<numero>`
- `POST /processes/documents/{folder_id}?process_number=<numero>`
- `GET /processes?process_number=<numero>`
- `GET /processes/thirds?process_number=<numero>`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type: application/vnd.gestiona.filter.files`

#### Modelo do corpo do pedido

```json
{
  "exact_code": "string"
}
```

#### Exemplo do corpo do pedido

```json
{
  "exact_code": "PROC-2026-001"
}
```

#### Dados da resposta utilizados

- `content[0].id`
  Utilizado como o id de processo Gestiona resolvido.

#### Notas

- `GET /processes?process_number=<numero>` devolve este valor diretamente no campo `result.Id` do gateway.
- Uma resposta `204 No Content` é tratada como não encontrada pelo fluxo atual de resolução por código de processo.
- Outros códigos de estado sem sucesso são propagados como falhas upstream pelos serviços do gateway.

### 2. POST `/uploads`

Cria um espaço temporário de upload no Gestiona antes de carregar conteúdo de documento DIGITAL.

#### Utilizado por

- `POST /processes/documents?process_number=<numero>` quando `documentSourceType` é `DIGITAL`
- `POST /processes/{process_id}/documents` quando `documentSourceType` é `DIGITAL`
- `POST /processes/documents/{folder_id}?process_number=<numero>` quando `documentSourceType` é `DIGITAL`
- `POST /processes/{process_id}/documents/{folder_id}` quando `documentSourceType` é `DIGITAL`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type: application/vnd.gestiona.file-document+json; version=4`

#### Dados da resposta utilizados

- Cabeçalho de resposta `Location`
  Utilizado como `{upload_location}` para o passo de upload seguinte e como origem da ligação para o conteúdo do documento criado.

#### Cabeçalhos de resposta observados

- `X-Gestiona-Deprecated`
  Registado em log quando o Gestiona o devolve para o tipo de média `application/vnd.gestiona.file-document+json; version=4`.

### 3. PUT `{upload_location}`

Carrega o conteúdo binário de um documento DIGITAL para a localização temporária de upload devolvida por `POST /uploads`.

#### Utilizado por

- `POST /processes/documents?process_number=<numero>` quando `documentSourceType` é `DIGITAL`
- `POST /processes/{process_id}/documents` quando `documentSourceType` é `DIGITAL`
- `POST /processes/documents/{folder_id}?process_number=<numero>` quando `documentSourceType` é `DIGITAL`
- `POST /processes/{process_id}/documents/{folder_id}` quando `documentSourceType` é `DIGITAL`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type: application/octet-stream`

#### Parâmetros da rota

- `upload_location` obrigatório
  Pode ser uma URL absoluta devolvida pelo Gestiona ou uma URL relativa resolvida contra `Gestiona:GestionaApiBaseUrl`.

#### Modelo do corpo do pedido

- Bytes binários brutos do documento

#### Dados da resposta utilizados

- Apenas o código de estado.

### 4. POST `/files/{file_id}/documents-and-folders`

Cria um documento ou pasta diretamente sob um processo Gestiona.

#### Utilizado por

- `POST /processes/documents?process_number=<numero>`
- `POST /processes/{process_id}/documents`

#### Parâmetros da rota

- `file_id` obrigatório
  O id do processo Gestiona. É fornecido como `process_id` ou resolvido a partir de `process_number` usando `GET /files`.

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type` depende do tipo de origem do documento:
  - `application/vnd.gestiona.file-document+json; version=4` para `DIGITAL` e `EXTERNAL_URL`
  - `application/vnd.gestiona.file-folder` para `FOLDER`

#### Modelo do corpo do pedido DIGITAL

```json
{
  "name": "string",
  "type": "DIGITAL",
  "metadata_language": "ES",
  "links": [
    {
      "rel": "content",
      "href": "string"
    }
  ]
}
```

#### Modelo do corpo do pedido EXTERNAL_URL

```json
{
  "name": "string",
  "type": "EXTERNAL_URL",
  "metadata_language": "ES",
  "external_url": "string"
}
```

#### Modelo do corpo do pedido FOLDER

```json
{
  "name": "string",
  "line": "1"
}
```

#### Dados da resposta utilizados

- O corpo da resposta da entidade criada é desserializado como `CreateDocumentAndFolderResponse`.
- `id` é utilizado quando está presente.
- Se `id` estiver ausente, o gateway resolve o id da entidade criada a partir do último segmento da ligação upstream `self`.
- `creation_date` e `modification_date` são devolvidos aos clientes do gateway depois da formatação como timestamp Unix.

#### Cabeçalhos de resposta observados

- `X-Gestiona-Deprecated`
  Registado em log para a criação de documentos `DIGITAL` e `EXTERNAL_URL` quando o Gestiona o devolve para o tipo de média file-document.

### 5. POST `/files/{file_id}/documents-and-folders/{folder_or_document_id}`

Cria um documento ou pasta dentro de uma pasta Gestiona.

#### Utilizado por

- `POST /processes/documents/{folder_id}?process_number=<numero>`
- `POST /processes/{process_id}/documents/{folder_id}`

#### Parâmetros da rota

- `file_id` obrigatório
  O id do processo Gestiona. É fornecido como `process_id` ou resolvido a partir de `process_number` usando `GET /files`.
- `folder_id` obrigatório
  O id da pasta Gestiona que recebe o novo documento ou pasta filha.

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type` depende do tipo de origem do documento:
  - `application/vnd.gestiona.file-document+json; version=4` para `DIGITAL` e `EXTERNAL_URL`
  - `application/vnd.gestiona.file-folder` para `FOLDER`

#### Modelo do corpo do pedido

Utiliza os mesmos modelos de corpo do pedido `DIGITAL`, `EXTERNAL_URL` e `FOLDER` documentados na chamada 4.

#### Dados da resposta utilizados

- O corpo da resposta da entidade criada é desserializado como `CreateDocumentAndFolderResponse`.
- `id` é utilizado quando está presente.
- Se `id` estiver ausente, o gateway resolve o id da entidade criada a partir do último segmento da ligação upstream `self`.
- `creation_date` e `modification_date` são devolvidos aos clientes do gateway depois da formatação como timestamp Unix.

#### Cabeçalhos de resposta observados

- `X-Gestiona-Deprecated`
  Registado em log para a criação de documentos `DIGITAL` e `EXTERNAL_URL` quando o Gestiona o devolve para o tipo de média file-document.

### 6. GET `/files/{file_id}/documents-and-folders`

Lista os documentos e pastas diretamente associados a um processo Gestiona.

#### Utilizado por

- `GET /processes/{process_id}/documents`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `content[].type`
- `content[].links[]`
  O gateway mapeia o `rel` relevante para `name` e o último segmento do caminho de `href` para `id`.

### 7. GET `/files/{file_id}/documents-and-folders/{folder_or_document_id}`

Lista os documentos e pastas dentro de uma pasta ou contentor de documento específico do Gestiona.

#### Utilizado por

- `GET /processes/{process_id}/documents/{document_id}`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- Os mesmos dados da listagem de documentos descritos na chamada 6.

### 8. GET `/content/small/documentinstances/{document_id}`

Descarrega um documento do Gestiona.

#### Utilizado por

- `GET /documents/{document_id}`

#### Parâmetros da rota

- `document_id` obrigatório

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Modelo do corpo do pedido

- nenhum

#### Corpo da resposta utilizado

- Conteúdo binário bruto do documento.

#### Cabeçalhos de resposta utilizados

- `Content-Disposition`
  Utilizado para resolver o nome do ficheiro de download.
- `Content-Type`
  Utilizado como o tipo de conteúdo da resposta do gateway.
- `X-Gestiona-Storage-Size`
- `X-Gestiona-Storage-Extension`
- `X-Gestiona-Storage-MIME-Type`
- `X-Gestiona-Storage-MD5`
- `X-Gestiona-Storage-SHA1`
- `X-Gestiona-Storage-SHA512`

### 9. GET `/files/{file_id}/thirdparties`

Obtém ligações de terceiros associadas a um processo Gestiona.

#### Utilizado por

- `GET /processes/thirds?process_number=<numero>`
- `GET /processes/{process_id}/thirds`

#### Parâmetros da rota

- `file_id` obrigatório
  O id do processo Gestiona. É fornecido como `process_id` ou resolvido a partir de `process_number` usando `GET /files`.

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Modelo do corpo do pedido

- nenhum

#### Dados da resposta utilizados

- Entradas `content[].links[]` em que `rel` é `third`.
- O gateway extrai o id do terceiro a partir do último segmento do caminho de cada `href` correspondente.
- O valor devolvido pelo gateway junta os ids extraídos com `;`.

### 10. GET `/thirds`

Resolve um id de terceiro Gestiona a partir de um NIF.

#### Utilizado por

- `GET /thirds?nif=<nif>`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type: application/vnd.gestiona.filter.thirds+json`

#### Modelo do corpo do pedido

```json
{
  "nif": "string"
}
```

#### Exemplo do corpo do pedido

```json
{
  "nif": "196510880"
}
```

#### Dados da resposta utilizados

- `content`
  Deve conter exatamente um item.
- `content[0].id`
  Utilizado como o id de terceiro Gestiona resolvido.

### 11. GET `/thirds/{third_id}`

Obtém um terceiro do Gestiona.

#### Utilizado por

- `GET /thirds?nif=<nif>` depois de resolver o id do terceiro com `GET /thirds`
- `GET /thirds/{third_id}`

#### Parâmetros da rota

- `third_id` obrigatório

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Modelo do corpo do pedido

- nenhum

#### Dados da resposta utilizados

- O corpo da resposta é desserializado como `Third`.
- Os campos de morada são enriquecidos pela chamada seguinte `GET /thirds/{third_id}/default-address`.

### 12. GET `/thirds/{third_id}/default-address`

Obtém a morada predefinida de um terceiro Gestiona.

#### Utilizado por

- `GET /thirds?nif=<nif>` depois de resolver e obter o terceiro
- `GET /thirds/{third_id}` depois de obter o terceiro

#### Parâmetros da rota

- `third_id` obrigatório

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Modelo do corpo do pedido

- nenhum

#### Dados da resposta utilizados

- O corpo da resposta é desserializado como `ThirdDefaultAddress`.
- O gateway combina estes campos de morada no resultado do terceiro:
  - `address`
  - `number`
  - `zip_code`
  - `province`
  - `country`
  - `type_of_road`
  - `zone`
- O gateway também lê a entrada `links` da morada em que `rel` é `parish` e mapeia o último segmento de `href` para o campo `parish_code` do resultado do terceiro.

### 13. POST `/catalog-2015/procedures/{activity_id}/external-procedures/{procedure_id}/create-file`

Cria um processo Gestiona para uma atividade de catálogo e um procedimento externo.

#### Utilizado por

- `POST /processes`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Modelo do corpo do pedido

- Array de bytes vazio

#### Dados da resposta utilizados

- `entry_date`
- Ligação em que `rel` é `file-open`, utilizada como `{file_open_href}` na chamada 15 e para extrair o id do processo utilizado na chamada 14.

### 14. GET `/files/{file_id}/selectable-titles`

Obtém os títulos selecionáveis para um processo Gestiona criado.

#### Utilizado por

- `POST /processes`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `selectable_titles`: o primeiro valor não vazio é enviado como `selectable_title` na chamada 15.
- `required`: desserializado, mas não devolvido diretamente pelo gateway.

### 15. POST `{file_open_href}`

Abre o processo Gestiona criado.

#### Utilizado por

- `POST /processes`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type: application/vnd.gestiona.file-opening+json; version=1`

#### Parâmetros da rota

- `file_open_href` obrigatório. Pode ser uma URL absoluta devolvida pelo Gestiona ou uma URL relativa resolvida contra `Gestiona:GestionaApiBaseUrl`.

#### Modelo do corpo do pedido

```json
{
  "entry_date": "string",
  "free_title": "string",
  "selectable_title": "string | null",
  "initial_assignation": [{ "href": "string" }],
  "links": [{ "rel": "management-unit-group", "href": "string" }]
}
```

#### Dados da resposta utilizados

- `id`
- `code`

### 16. POST `/files/{file_id}/related-files`

Cria uma ligação entre dois processos Gestiona relacionados.

#### Utilizado por

- `POST /processes/related`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type: application/vnd.gestiona.links+json`

#### Modelo do corpo do pedido

```json
{
  "links": [{ "rel": "related-files", "href": "string" }]
}
```

#### Dados da resposta utilizados

- Apenas o código de estado.

### 17. GET `/files/{file_id}/related-files`

Obtém os processos Gestiona relacionados.

#### Utilizado por

- `GET /processes/{process_id}/related`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `content[].id`
- `content[].code`

### 18. DELETE `/files/{file_id}/related-files/{related_file_id}`

Elimina uma ligação entre processos relacionados.

#### Utilizado por

- `DELETE /processes/{process_id}/related/{related_process_id}`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- Apenas o código de estado.

### 19. GET `/catalog-2015/procedures`

Obtém as atividades disponíveis no catálogo Gestiona.

#### Utilizado por

- `GET /activities`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `content[].id`
- `content[].name`

### 20. GET `/catalog-2015/procedures/{activity_id}/external-procedures`

Obtém os procedimentos externos de uma atividade do catálogo Gestiona.

#### Utilizado por

- `GET /activities/{activity_id}/procedures`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `content[].id`
- `content[].title`, devolvido pelo gateway como `name`.

### 21. GET `/files/{file_id}/documents/{document_id}/signatures`

Obtém as assinaturas de um documento num processo Gestiona.

#### Utilizado por

- `GET /processes/{process_id}/documents/{document_id}/signatures`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `content[].date`
- `content[].signature_state`
- `content[].links[]` em que `rel` é `signed-user` ou `signer-user`. O gateway segue esta ligação com a chamada 22.

### 22. GET `{signer_user_href}`

Obtém um utilizador Gestiona referenciado por uma assinatura de documento.

#### Utilizado por

- `GET /processes/{process_id}/documents/{document_id}/signatures`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Parâmetros da rota

- `signer_user_href` obrigatório. Pode ser uma URL absoluta devolvida pelo Gestiona ou uma URL relativa resolvida contra `Gestiona:GestionaApiBaseUrl`.

#### Dados da resposta utilizados

- `username`
- `name`

### 23. GET `/files/assignees/users`

Obtém o primeiro utilizador destinatário do Gestiona que corresponde a um nome de utilizador.

#### Utilizado por

- `GET /processes/assignees/users`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type: application/vnd.gestiona.filter.assignees+json`

#### Modelo do corpo do pedido

```json
{
  "username": "string"
}
```

#### Dados da resposta utilizados

- Primeiro item de `content`.
- `id`, `username` e `name`.

### 24. GET `/files/assignees/groups`

Obtém os grupos destinatários do Gestiona disponíveis para atribuição de processos.

#### Utilizado por

- `GET /processes/assignees/groups`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `content[].id`
- `content[].name`

### 25. GET `/connectors`

Obtém os conectores de filas do Gestiona.

#### Utilizado por

- `GET /queues/connectors/{connector_name}`
- `GET /queues/connectors/{connector_name}/{message_id}`
- `POST /queues/connectors/{connector_name}/{message_id}`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `content[].code`. O gateway verifica se um código de conector corresponde a `{connector_name}`.

### 26. GET `/queues/subscriptions`

Obtém as subscrições ativas das filas Gestiona.

#### Utilizado por

- Endpoints de obtenção e resposta de mensagens dos conectores de filas.

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `content[].name`. O gateway procura `connectors#{connector_name}`.

### 27. POST `/queues/connectors/{connector_name}/subscription`

Subscreve o gateway numa fila de conector Gestiona quando não existe uma subscrição ativa.

#### Utilizado por

- Endpoints de obtenção e resposta de mensagens dos conectores de filas.

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Modelo do corpo do pedido

- Array de bytes vazio

#### Dados da resposta utilizados

- Código de estado.
- A `description` da resposta de erro, quando presente, é propagada como mensagem de erro do gateway.

### 28. POST `/queues/connectors/{connector_name}?max-messages=100&hold-seconds=1`

Obtém mensagens pendentes de uma fila de conector Gestiona.

#### Utilizado por

- `GET /queues/connectors/{connector_name}`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório

#### Dados da resposta utilizados

- `content[].payload.target`, devolvido como `message_id`.
- `content[].entry`, formatado e devolvido como `date_signed`.

### 29. GET `/queues/connectors/{connector_name}/{message_id}`

Obtém uma mensagem individual de uma fila de conector Gestiona.

#### Utilizado por

- `GET /queues/connectors/{connector_name}/{message_id}`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Accept: application/vnd.gestiona.queues.message`

#### Dados da resposta utilizados

- `payload.target`, devolvido como `message_id`.
- `entry`, formatado e devolvido como `date_signed`.

### 30. POST `/queues/connectors/{connector_name}/{message_id}`

Envia uma resposta para uma mensagem de uma fila de conector Gestiona.

#### Utilizado por

- `POST /queues/connectors/{connector_name}/{message_id}`

#### Cabeçalhos do pedido

- `X-Gestiona-Access-Token` obrigatório
- `Content-Type: application/vnd.gestiona.connector-response+json`

#### Modelo do corpo do pedido

```json
{
  "result_success": "string",
  "message": "string | null"
}
```

#### Dados da resposta utilizados

- Código de estado.
- A `description` da resposta de erro, quando presente, é propagada como mensagem de erro do gateway.

### 31. POST `/addon/authorizations`

Cria um pedido de autorização de add-on.

#### Utilizado por

- `POST /addon/authorizations`

#### Cabeçalhos do pedido

- `X-Gestiona-Addon-Token` obrigatório; valor de `Gestiona:AddonToken`

#### Modelo do corpo do pedido

- Array de bytes vazio

#### Dados da resposta utilizados

- Cabeçalho `Location`. O último segmento do URL é devolvido pelo gateway como `authId`.
- Um cabeçalho `Location` ausente ou inválido é tratado como falha upstream.

### 32. GET `/addon/authorizations/{auth_id}`

Verifica se uma autorização de add-on está pendente ou autorizada.

#### Utilizado por

- `GET /addon/authorizations/{auth_id}`

#### Cabeçalhos do pedido

- `X-Gestiona-Addon-Token` obrigatório; valor de `Gestiona:AddonToken`

#### Dados da resposta utilizados

- HTTP `401`: a autorização está pendente. O cabeçalho `Location` é devolvido como `authorizeUrl`.
- HTTP `200`: a autorização está concluída. Os campos `user_id` e `access_token` são devolvidos como `authorizedInfo.userId` e `authorizedInfo.accessToken`.
- Outros códigos de estado sem sucesso são propagados como erros do gateway.
