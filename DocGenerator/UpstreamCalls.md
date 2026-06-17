# Gestiona Upstream Calls Documentation

<center>Versao 1.1.1</center>

## Index

### Common behavior

- [Base URL](#base-url)
- [Authentication header](#authentication-header)

### Upstream calls

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

## Common behavior

### Base URL

All relative upstream routes are sent to the configured Gestiona API base URL:

- Configuration key: `Gestiona:GestionaApiBaseUrl`
- The client normalizes the base URL by appending a trailing `/` when needed.

### Authentication header

All Gestiona requests include:

- `X-Gestiona-Access-Token`

The token value is resolved by the gateway service layer:

1. Use the inbound API request header `X-User-Access-Token` when present and not blank.
2. Otherwise use the configured token from `Gestiona:AccessToken`.

## Upstream calls

### 1. GET `/files`

Resolves a Gestiona file id from a process number/code.

#### Used by

- `POST /processes/documents?process_number=<numero>`
- `POST /processes/documents/{folder_id}?process_number=<numero>`
- `GET /processes?process_number=<numero>`
- `GET /processes/thirds?process_number=<numero>`

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type: application/vnd.gestiona.filter.files`

#### Request body model

```json
{
  "exact_code": "string"
}
```

#### Request body example

```json
{
  "exact_code": "PROC-2026-001"
}
```

#### Response data used

- `content[0].id`
  Used as the resolved Gestiona file id.

#### Notes

- `GET /processes?process_number=<numero>` returns this value directly in the gateway `result.Id` field.
- A `204 No Content` response is treated as not found by the current process-code resolution flow.
- Other unsuccessful status codes are propagated as upstream failures by gateway services.

### 2. POST `/uploads`

Creates a temporary Gestiona upload space before uploading DIGITAL document content.

#### Used by

- `POST /processes/documents?process_number=<numero>` when `documentSourceType` is `DIGITAL`
- `POST /processes/{process_id}/documents` when `documentSourceType` is `DIGITAL`
- `POST /processes/documents/{folder_id}?process_number=<numero>` when `documentSourceType` is `DIGITAL`
- `POST /processes/{process_id}/documents/{folder_id}` when `documentSourceType` is `DIGITAL`

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type: application/vnd.gestiona.file-document+json; version=4`

#### Request body model

- Empty byte array

#### Response data used

- `Location` response header
  Used as `{upload_location}` for the next upload step and as the source for the created document content link.

#### Response headers observed

- `X-Gestiona-Deprecated`
  Logged when Gestiona returns it for the `application/vnd.gestiona.file-document+json; version=4` media type.

### 3. PUT `{upload_location}`

Uploads the binary content for a DIGITAL document to the temporary upload location returned by `POST /uploads`.

#### Used by

- `POST /processes/documents?process_number=<numero>` when `documentSourceType` is `DIGITAL`
- `POST /processes/{process_id}/documents` when `documentSourceType` is `DIGITAL`
- `POST /processes/documents/{folder_id}?process_number=<numero>` when `documentSourceType` is `DIGITAL`
- `POST /processes/{process_id}/documents/{folder_id}` when `documentSourceType` is `DIGITAL`

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type: application/octet-stream`

#### Route parameters

- `upload_location` required
  Can be an absolute URL returned by Gestiona or a relative URL resolved against `Gestiona:GestionaApiBaseUrl`.

#### Request body model

- Raw binary document bytes

#### Response data used

- Status code only.

### 4. POST `/files/{file_id}/documents-and-folders`

Creates a document or folder directly under a Gestiona file.

#### Used by

- `POST /processes/documents?process_number=<numero>`
- `POST /processes/{process_id}/documents`

#### Route parameters

- `file_id` required
  The Gestiona file id. It is either provided as `process_id` or resolved from `process_number` using `GET /files`.

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type` depends on the document source type:
  - `application/vnd.gestiona.file-document+json; version=4` for `DIGITAL` and `EXTERNAL_URL`
  - `application/vnd.gestiona.file-folder` for `FOLDER`

#### DIGITAL request body model

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

#### EXTERNAL_URL request body model

```json
{
  "name": "string",
  "type": "EXTERNAL_URL",
  "metadata_language": "ES",
  "external_url": "string"
}
```

#### FOLDER request body model

```json
{
  "name": "string",
  "line": "1"
}
```

#### Response data used

- Created entity response body is deserialized as `CreateDocumentAndFolderResponse`.
- `id` is used when present.
- If `id` is absent, the gateway resolves the created entity id from the last segment of the upstream `self` link.
- `creation_date` and `modification_date` are returned to gateway clients after Unix timestamp formatting.

#### Response headers observed

- `X-Gestiona-Deprecated`
  Logged for `DIGITAL` and `EXTERNAL_URL` document creation when Gestiona returns it for the file-document media type.

### 5. POST `/files/{file_id}/documents-and-folders/{folder_id}`

Creates a document or folder inside a Gestiona folder.

#### Used by

- `POST /processes/documents/{folder_id}?process_number=<numero>`
- `POST /processes/{process_id}/documents/{folder_id}`

#### Route parameters

- `file_id` required
  The Gestiona file id. It is either provided as `process_id` or resolved from `process_number` using `GET /files`.
- `folder_id` required
  The Gestiona folder id that receives the new document or child folder.

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type` depends on the document source type:
  - `application/vnd.gestiona.file-document+json; version=4` for `DIGITAL` and `EXTERNAL_URL`
  - `application/vnd.gestiona.file-folder` for `FOLDER`

#### Request body model

Uses the same `DIGITAL`, `EXTERNAL_URL`, and `FOLDER` request body models documented in call 4.

#### Response data used

- Created entity response body is deserialized as `CreateDocumentAndFolderResponse`.
- `id` is used when present.
- If `id` is absent, the gateway resolves the created entity id from the last segment of the upstream `self` link.
- `creation_date` and `modification_date` are returned to gateway clients after Unix timestamp formatting.

#### Response headers observed

- `X-Gestiona-Deprecated`
  Logged for `DIGITAL` and `EXTERNAL_URL` document creation when Gestiona returns it for the file-document media type.

### 6. GET `/content/small/documentinstances/{document_id}`

Downloads a document from Gestiona.

#### Used by

- `GET /documents/{document_id}`

#### Route parameters

- `document_id` required

#### Request headers

- `X-Gestiona-Access-Token` required

#### Request body model

- none

#### Response body used

- Raw binary document content.

#### Response headers used

- `Content-Disposition`
  Used to resolve the download file name.
- `Content-Type`
  Used as the gateway response content type.
- `X-Gestiona-Storage-Size`
- `X-Gestiona-Storage-Extension`
- `X-Gestiona-Storage-MIME-Type`
- `X-Gestiona-Storage-MD5`
- `X-Gestiona-Storage-SHA1`
- `X-Gestiona-Storage-SHA512`

### 7. GET `/files/{file_id}/thirdparties`

Gets third-party links associated with a Gestiona file.

#### Used by

- `GET /processes/thirds?process_number=<numero>`
- `GET /processes/{process_id}/thirds`

#### Route parameters

- `file_id` required
  The Gestiona file id. It is either provided as `process_id` or resolved from `process_number` using `GET /files`.

#### Request headers

- `X-Gestiona-Access-Token` required

#### Request body model

- none

#### Response data used

- `content[].links[]` entries where `rel` is `third`.
- The gateway extracts the third id from the last path segment of each matching `href`.
- The returned gateway value joins the extracted ids with `;`.

### 8. GET `/thirds`

Resolves a Gestiona third id from a NIF.

#### Used by

- `GET /thirds?nif=<nif>`

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type: application/vnd.gestiona.filter.thirds+json`

#### Request body model

```json
{
  "nif": "string"
}
```

#### Request body example

```json
{
  "nif": "196510880"
}
```

#### Response data used

- `content`
  Must contain exactly one item.
- `content[0].id`
  Used as the resolved Gestiona third id.

### 9. GET `/thirds/{third_id}`

Gets a third from Gestiona.

#### Used by

- `GET /thirds?nif=<nif>` after resolving the third id with `GET /thirds`
- `GET /thirds/{third_id}`

#### Route parameters

- `third_id` required

#### Request headers

- `X-Gestiona-Access-Token` required

#### Request body model

- none

#### Response data used

- The response body is deserialized as `Third`.
- Address fields are enriched by the next `GET /thirds/{third_id}/default-address` call.

### 10. GET `/thirds/{third_id}/default-address`

Gets the default address for a Gestiona third.

#### Used by

- `GET /thirds?nif=<nif>` after resolving and retrieving the third
- `GET /thirds/{third_id}` after retrieving the third

#### Route parameters

- `third_id` required

#### Request headers

- `X-Gestiona-Access-Token` required

#### Request body model

- none

#### Response data used

- The response body is deserialized as `ThirdDefaultAddress`.
- The gateway merges these address fields into the third result:
  - `address`
  - `number`
  - `zip_code`
  - `province`
  - `country`
  - `type_of_road`
  - `zone`
- The gateway also reads the address `links` entry where `rel` is `parish` and maps the last `href` segment to third result field `parish_code`.
