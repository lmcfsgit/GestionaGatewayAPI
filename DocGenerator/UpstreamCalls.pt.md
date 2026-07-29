# Documentacao das Chamadas Upstream do Gestiona

<center>Versao 1.1.1</center>

## Indice

### Comportamento comum

- [URL base](#url-base)
- [Cabecalho de autenticacao](#cabecalho-de-autenticacao)

### Chamadas upstream

- [1. GET `/files`](#1-get-files)
- [2. POST `/uploads`](#2-post-uploads)
- [3. PUT `{upload_location}`](#3-put-upload_location)
- [4. POST `/files/{file_id}/documents-and-folders`](#4-post-filesfile_iddocuments-and-folders)
- [5. POST `/files/{file_id}/documents-and-folders/{folder_id}`](#5-post-filesfile_iddocuments-and-foldersfolder_id)
- [6. GET `/content/small/documentinstances/{document_id}`](#6-get-contentsmalldocumentinstancesdocument_id)
- [7. GET `/files/{file_id}/thirdparties`](#7-get-filesfile_idthirdparties)
- [8. GET `/thirds`](#8-get-thirds)
- [9. GET `/thirds/{third_id}`](#9-get-thirdsthird_id)
- [10. GET `/thirds/{third_id}/default-address`](#10-get-thirdsthird_iddefault-address)

## Comportamento comum

### URL base

Todas as rotas upstream relativas sao enviadas para a URL base configurada da API Gestiona:

- Chave de configuracao: `Gestiona:GestionaApiBaseUrl`
- O cliente normaliza a URL base adicionando uma `/` final quando necessario.

### Cabecalho de autenticacao

Todos os pedidos ao Gestiona incluem:

- `X-Gestiona-Access-Token`

O valor do token e resolvido pela camada de servico do gateway:

1. Utiliza o cabecalho do pedido de API de entrada `X-User-Access-Token` quando esta presente e nao esta em branco.
2. Caso contrario, utiliza o token configurado em `Gestiona:AccessToken`.

## Chamadas upstream

### 1. GET `/files`

Resolve um id de processo Gestiona a partir de um numero/codigo de processo.

#### Utilizado por

- `POST /processes/documents?process_number=<numero>`
- `POST /processes/documents/{folder_id}?process_number=<numero>`
- `GET /processes?process_number=<numero>`
- `GET /processes/thirds?process_number=<numero>`

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio
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
- Uma resposta `204 No Content` e tratada como nao encontrada pelo fluxo atual de resolucao por codigo de processo.
- Outros codigos de estado sem sucesso sao propagados como falhas upstream pelos servicos do gateway.

### 2. POST `/uploads`

Cria um espaco temporario de upload no Gestiona antes de carregar conteudo de documento DIGITAL.

#### Utilizado por

- `POST /processes/documents?process_number=<numero>` quando `documentSourceType` e `DIGITAL`
- `POST /processes/{process_id}/documents` quando `documentSourceType` e `DIGITAL`
- `POST /processes/documents/{folder_id}?process_number=<numero>` quando `documentSourceType` e `DIGITAL`
- `POST /processes/{process_id}/documents/{folder_id}` quando `documentSourceType` e `DIGITAL`

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio
- `Content-Type: application/vnd.gestiona.file-document+json; version=4`

#### Modelo do corpo do pedido

- Array de bytes vazio

#### Dados da resposta utilizados

- Cabecalho de resposta `Location`
  Utilizado como `{upload_location}` para o passo de upload seguinte e como origem da ligacao para o conteudo do documento criado.

#### Cabecalhos de resposta observados

- `X-Gestiona-Deprecated`
  Registado em log quando o Gestiona o devolve para o tipo de media `application/vnd.gestiona.file-document+json; version=4`.

### 3. PUT `{upload_location}`

Carrega o conteudo binario de um documento DIGITAL para a localizacao temporaria de upload devolvida por `POST /uploads`.

#### Utilizado por

- `POST /processes/documents?process_number=<numero>` quando `documentSourceType` e `DIGITAL`
- `POST /processes/{process_id}/documents` quando `documentSourceType` e `DIGITAL`
- `POST /processes/documents/{folder_id}?process_number=<numero>` quando `documentSourceType` e `DIGITAL`
- `POST /processes/{process_id}/documents/{folder_id}` quando `documentSourceType` e `DIGITAL`

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio
- `Content-Type: application/octet-stream`

#### Parametros da rota

- `upload_location` obrigatorio
  Pode ser uma URL absoluta devolvida pelo Gestiona ou uma URL relativa resolvida contra `Gestiona:GestionaApiBaseUrl`.

#### Modelo do corpo do pedido

- Bytes binarios brutos do documento

#### Dados da resposta utilizados

- Apenas o codigo de estado.

### 4. POST `/files/{file_id}/documents-and-folders`

Cria um documento ou pasta diretamente sob um processo Gestiona.

#### Utilizado por

- `POST /processes/documents?process_number=<numero>`
- `POST /processes/{process_id}/documents`

#### Parametros da rota

- `file_id` obrigatorio
  O id do processo Gestiona. E fornecido como `process_id` ou resolvido a partir de `process_number` usando `GET /files`.

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio
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

- O corpo da resposta da entidade criada e desserializado como `CreateDocumentAndFolderResponse`.
- `id` e utilizado quando esta presente.
- Se `id` estiver ausente, o gateway resolve o id da entidade criada a partir do ultimo segmento da ligacao upstream `self`.
- `creation_date` e `modification_date` sao devolvidos aos clientes do gateway depois da formatacao como timestamp Unix.

#### Cabecalhos de resposta observados

- `X-Gestiona-Deprecated`
  Registado em log para a criacao de documentos `DIGITAL` e `EXTERNAL_URL` quando o Gestiona o devolve para o tipo de media file-document.

### 5. POST `/files/{file_id}/documents-and-folders/{folder_id}`

Cria um documento ou pasta dentro de uma pasta Gestiona.

#### Utilizado por

- `POST /processes/documents/{folder_id}?process_number=<numero>`
- `POST /processes/{process_id}/documents/{folder_id}`

#### Parametros da rota

- `file_id` obrigatorio
  O id do processo Gestiona. E fornecido como `process_id` ou resolvido a partir de `process_number` usando `GET /files`.
- `folder_id` obrigatorio
  O id da pasta Gestiona que recebe o novo documento ou pasta filha.

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio
- `Content-Type` depende do tipo de origem do documento:
  - `application/vnd.gestiona.file-document+json; version=4` para `DIGITAL` e `EXTERNAL_URL`
  - `application/vnd.gestiona.file-folder` para `FOLDER`

#### Modelo do corpo do pedido

Utiliza os mesmos modelos de corpo do pedido `DIGITAL`, `EXTERNAL_URL` e `FOLDER` documentados na chamada 4.

#### Dados da resposta utilizados

- O corpo da resposta da entidade criada e desserializado como `CreateDocumentAndFolderResponse`.
- `id` e utilizado quando esta presente.
- Se `id` estiver ausente, o gateway resolve o id da entidade criada a partir do ultimo segmento da ligacao upstream `self`.
- `creation_date` e `modification_date` sao devolvidos aos clientes do gateway depois da formatacao como timestamp Unix.

#### Cabecalhos de resposta observados

- `X-Gestiona-Deprecated`
  Registado em log para a criacao de documentos `DIGITAL` e `EXTERNAL_URL` quando o Gestiona o devolve para o tipo de media file-document.

### 6. GET `/content/small/documentinstances/{document_id}`

Descarrega um documento do Gestiona.

#### Utilizado por

- `GET /documents/{document_id}`

#### Parametros da rota

- `document_id` obrigatorio

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio

#### Modelo do corpo do pedido

- nenhum

#### Corpo da resposta utilizado

- Conteudo binario bruto do documento.

#### Cabecalhos de resposta utilizados

- `Content-Disposition`
  Utilizado para resolver o nome do ficheiro de download.
- `Content-Type`
  Utilizado como o tipo de conteudo da resposta do gateway.
- `X-Gestiona-Storage-Size`
- `X-Gestiona-Storage-Extension`
- `X-Gestiona-Storage-MIME-Type`
- `X-Gestiona-Storage-MD5`
- `X-Gestiona-Storage-SHA1`
- `X-Gestiona-Storage-SHA512`

### 7. GET `/files/{file_id}/thirdparties`

Obtem ligacoes de terceiros associadas a um processo Gestiona.

#### Utilizado por

- `GET /processes/thirds?process_number=<numero>`
- `GET /processes/{process_id}/thirds`

#### Parametros da rota

- `file_id` obrigatorio
  O id do processo Gestiona. E fornecido como `process_id` ou resolvido a partir de `process_number` usando `GET /files`.

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio

#### Modelo do corpo do pedido

- nenhum

#### Dados da resposta utilizados

- Entradas `content[].links[]` em que `rel` e `third`.
- O gateway extrai o id do terceiro a partir do ultimo segmento do caminho de cada `href` correspondente.
- O valor devolvido pelo gateway junta os ids extraidos com `;`.

### 8. GET `/thirds`

Resolve um id de terceiro Gestiona a partir de um NIF.

#### Utilizado por

- `GET /thirds?nif=<nif>`

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio
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

### 9. GET `/thirds/{third_id}`

Obtem um terceiro do Gestiona.

#### Utilizado por

- `GET /thirds?nif=<nif>` depois de resolver o id do terceiro com `GET /thirds`
- `GET /thirds/{third_id}`

#### Parametros da rota

- `third_id` obrigatorio

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio

#### Modelo do corpo do pedido

- nenhum

#### Dados da resposta utilizados

- O corpo da resposta e desserializado como `Third`.
- Os campos de morada sao enriquecidos pela chamada seguinte `GET /thirds/{third_id}/default-address`.

### 10. GET `/thirds/{third_id}/default-address`

Obtem a morada predefinida de um terceiro Gestiona.

#### Utilizado por

- `GET /thirds?nif=<nif>` depois de resolver e obter o terceiro
- `GET /thirds/{third_id}` depois de obter o terceiro

#### Parametros da rota

- `third_id` obrigatorio

#### Cabecalhos do pedido

- `X-Gestiona-Access-Token` obrigatorio

#### Modelo do corpo do pedido

- nenhum

#### Dados da resposta utilizados

- O corpo da resposta e desserializado como `ThirdDefaultAddress`.
- O gateway combina estes campos de morada no resultado do terceiro:
  - `address`
  - `number`
  - `zip_code`
  - `province`
  - `country`
  - `type_of_road`
  - `zone`
- O gateway tambem le a entrada `links` da morada em que `rel` e `parish` e mapeia o ultimo segmento de `href` para o campo `parish_code` do resultado do terceiro.
