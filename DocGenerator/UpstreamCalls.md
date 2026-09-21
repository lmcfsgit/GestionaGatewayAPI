# Gestiona Upstream Calls Documentation

<center>Versao 1.9.0</center>

## Index

### Common behavior

- [Base URL](#base-url)
- [Authentication header](#authentication-header)

### Upstream calls

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

Queue connector endpoints are an exception at the gateway-service level: they always use the configured token from `Gestiona:AccessToken`.

Add-on authorization calls use `X-Gestiona-Addon-Token` with the configured value from `Gestiona:AddonToken` instead of `X-Gestiona-Access-Token`.

## Upstream calls

### 1. GET `/files`

Resolves a Gestiona file id or self link from a process number/code.

#### Used by

- `GET /processes?process_number=<numero>`
- `POST /processes/documents?process_number=<numero>`
- `POST /processes/documents/{folder_id}?process_number=<numero>`
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

#### Response data used

- `content[0].id`
  Used as the resolved Gestiona file id.
- `content[].links[]` where `rel` is `self`
  Used when the service needs the upstream self href.

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

### 3. PUT `{upload_location}`

Uploads binary content for a DIGITAL document to the temporary upload location returned by `POST /uploads`.

#### Used by

- DIGITAL document creation endpoints.

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

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type` depends on the document source type:
  - `application/vnd.gestiona.file-document+json; version=4` for `DIGITAL` and `EXTERNAL_URL`
  - `application/vnd.gestiona.file-folder` for `FOLDER`

#### Request body models

DIGITAL:

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

EXTERNAL_URL:

```json
{
  "name": "string",
  "type": "EXTERNAL_URL",
  "metadata_language": "ES",
  "external_url": "string"
}
```

FOLDER:

```json
{
  "name": "string",
  "line": "1"
}
```

#### Response data used

- Created entity body deserialized as `CreateDocumentAndFolderResponse`.
- `id`, or the final path segment of the `self` link when `id` is absent.
- `creation_date` and `modification_date`.

### 5. POST `/files/{file_id}/documents-and-folders/{folder_or_document_id}`

Creates a document or folder inside a Gestiona folder.

#### Used by

- `POST /processes/documents/{folder_id}?process_number=<numero>`
- `POST /processes/{process_id}/documents/{folder_id}`

#### Request headers

- Same as call 4.

#### Request body model

- Same DIGITAL, EXTERNAL_URL, and FOLDER body models documented in call 4.

#### Response data used

- Same created entity fields documented in call 4.

### 6. GET `/files/{file_id}/documents-and-folders`

Lists documents and folders directly under a Gestiona file.

#### Used by

- `GET /processes/{process_id}/documents`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].type`
- `content[].links[]`
  The gateway maps the relevant link `rel` to `name` and the final `href` path segment to `id`.

### 7. GET `/files/{file_id}/documents-and-folders/{folder_or_document_id}`

Lists documents and folders under a specific Gestiona folder or document container.

#### Used by

- `GET /processes/{process_id}/documents/{document_id}`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- Same document-list data documented in call 6.

### 8. GET `/content/small/documentinstances/{document_id}`

Downloads a document from Gestiona.

#### Used by

- `GET /documents/{document_id}`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response body used

- Raw binary document content.

#### Response headers used

- `Content-Disposition`
- `Content-Type`
- `X-Gestiona-Storage-Size`
- `X-Gestiona-Storage-Extension`
- `X-Gestiona-Storage-MIME-Type`
- `X-Gestiona-Storage-MD5`
- `X-Gestiona-Storage-SHA1`
- `X-Gestiona-Storage-SHA512`

### 9. GET `/files/{file_id}/thirdparties`

Gets third-party links associated with a Gestiona file.

#### Used by

- `GET /processes/thirds?process_number=<numero>`
- `GET /processes/{process_id}/thirds`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].links[]` entries where `rel` is `third`.
- The gateway extracts the third id from the last path segment of each matching `href`.
- The returned gateway value joins the extracted ids with `;`.

### 10. GET `/thirds`

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

#### Response data used

- `content`
  Must contain exactly one item.
- `content[0].id`
  Used as the resolved Gestiona third id.

### 11. GET `/thirds/{third_id}`

Gets a third from Gestiona.

#### Used by

- `GET /thirds?nif=<nif>` after resolving the third id with `GET /thirds`
- `GET /thirds/{third_id}`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- Response body deserialized as `Third`.
- Address fields are enriched by `GET /thirds/{third_id}/default-address`.

### 12. GET `/thirds/{third_id}/default-address`

Gets the default address for a Gestiona third.

#### Used by

- `GET /thirds?nif=<nif>`
- `GET /thirds/{third_id}`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- Response body deserialized as `ThirdDefaultAddress`.
- Address fields merged into the third result.
- The `links` entry where `rel` is `parish`; its final `href` segment maps to `parish_code`.

### 13. POST `/catalog-2015/procedures/{activity_id}/external-procedures/{procedure_id}/create-file`

Creates a Gestiona process file for a catalog activity and external procedure.

#### Used by

- `POST /processes`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Request body model

- Empty byte array

#### Response data used

- `entry_date`
- Link where `rel` is `file-open`
  Used as `{file_open_href}` for call 15 and to extract the created file id for call 14.

### 14. GET `/files/{file_id}/selectable-titles`

Gets selectable titles for a created Gestiona process file.

#### Used by

- `POST /processes`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `selectable_titles`
  The first non-blank value is sent as `selectable_title` in call 15.
- `required`
  Deserialized but not returned directly by the gateway.

### 15. POST `{file_open_href}`

Opens the created Gestiona process file.

#### Used by

- `POST /processes`

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type: application/vnd.gestiona.file-opening+json; version=1`

#### Route parameters

- `file_open_href` required
  Can be an absolute URL returned by Gestiona or a relative URL resolved against `Gestiona:GestionaApiBaseUrl`.

#### Request body model

```json
{
  "entry_date": "string",
  "free_title": "string",
  "selectable_title": "string | null",
  "initial_assignation": [
    {
      "href": "string"
    }
  ],
  "links": [
    {
      "rel": "management-unit-group",
      "href": "string"
    }
  ]
}
```

#### Response data used

- `id`
- `code`

### 16. POST `/files/{file_id}/related-files`

Creates a related-file link from one Gestiona file to another.

#### Used by

- `POST /processes/related`

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type: application/vnd.gestiona.links+json`

#### Request body model

```json
{
  "links": [
    {
      "rel": "related-files",
      "href": "string"
    }
  ]
}
```

#### Response data used

- Status code only.

### 17. GET `/files/{file_id}/related-files`

Gets related Gestiona files.

#### Used by

- `GET /processes/{process_id}/related`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].id`
- `content[].code`

### 18. DELETE `/files/{file_id}/related-files/{related_file_id}`

Deletes a related-file link.

#### Used by

- `DELETE /processes/{process_id}/related/{related_process_id}`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- Status code only.

### 19. GET `/catalog-2015/procedures`

Gets activities available in the Gestiona catalog.

#### Used by

- `GET /activities`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].id`
- `content[].name`

### 20. GET `/catalog-2015/procedures/{activity_id}/external-procedures`

Gets external procedures for a Gestiona catalog activity.

#### Used by

- `GET /activities/{activity_id}/procedures`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].id`
- `content[].title`
  Returned by the gateway as `name`.

### 21. GET `/files/{file_id}/documents/{document_id}/signatures`

Gets signatures for a Gestiona document in a process file.

#### Used by

- `GET /processes/{process_id}/documents/{document_id}/signatures`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].date`
- `content[].signature_state`
- `content[].links[]` where `rel` is `signed-user` or `signer-user`
  The gateway follows this link with call 22.

### 22. GET `{signer_user_href}`

Gets a Gestiona user referenced by a document signature.

#### Used by

- `GET /processes/{process_id}/documents/{document_id}/signatures`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Route parameters

- `signer_user_href` required
  Can be an absolute URL returned by Gestiona or a relative URL resolved against `Gestiona:GestionaApiBaseUrl`.

#### Response data used

- `username`
- `name`

### 23. GET `/files/assignees/users`

Gets the first Gestiona assignee user matching a username.

#### Used by

- `GET /processes/assignees/users`

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type: application/vnd.gestiona.filter.assignees+json`

#### Request body model

```json
{
  "username": "string"
}
```

#### Response data used

- First item in `content`.
- `id`
- `username`
- `name`

### 24. GET `/files/assignees/groups`

Gets Gestiona assignee groups available for process assignment.

#### Used by

- `GET /processes/assignees/groups`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].id`
- `content[].name`

### 25. GET `/connectors`

Gets Gestiona queue connectors.

#### Used by

- `GET /queues/connectors/{connector_name}`
- `GET /queues/connectors/{connector_name}/{message_id}`
- `POST /queues/connectors/{connector_name}/{message_id}`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].code`
  The gateway verifies that one connector code matches `{connector_name}`.

### 26. GET `/queues/subscriptions`

Gets active Gestiona queue subscriptions.

#### Used by

- Queue connector message retrieval and response endpoints.

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].name`
  The gateway looks for `connectors#{connector_name}`.

### 27. POST `/queues/connectors/{connector_name}/subscription`

Subscribes the gateway to a Gestiona connector queue when no active subscription exists.

#### Used by

- Queue connector message retrieval and response endpoints.

#### Request headers

- `X-Gestiona-Access-Token` required

#### Request body model

- Empty byte array

#### Response data used

- Status code.
- Error response `description`, when present, is propagated as the gateway error message.

### 28. POST `/queues/connectors/{connector_name}?max-messages=100&hold-seconds=1`

Retrieves queued messages from a Gestiona connector queue.

#### Used by

- `GET /queues/connectors/{connector_name}`

#### Request headers

- `X-Gestiona-Access-Token` required

#### Response data used

- `content[].payload.target`
  Returned as `message_id`.
- `content[].entry`
  Formatted and returned as `date_signed`.

### 29. GET `/queues/connectors/{connector_name}/{message_id}`

Gets a single Gestiona connector queue message.

#### Used by

- `GET /queues/connectors/{connector_name}/{message_id}`

#### Request headers

- `X-Gestiona-Access-Token` required
- `Accept: application/vnd.gestiona.queues.message`

#### Response data used

- `payload.target`
  Returned as `message_id`.
- `entry`
  Formatted and returned as `date_signed`.

### 30. POST `/queues/connectors/{connector_name}/{message_id}`

Sends a response for a Gestiona connector queue message.

#### Used by

- `POST /queues/connectors/{connector_name}/{message_id}`

#### Request headers

- `X-Gestiona-Access-Token` required
- `Content-Type: application/vnd.gestiona.connector-response+json`

#### Request body model

```json
{
  "result_success": "string",
  "message": "string | null"
}
```

#### Response data used

- Status code.
- Error response `description`, when present, is propagated as the gateway error message.

### 31. POST `/addon/authorizations`

Creates an add-on authorization request.

#### Used by

- `POST /addon/authorizations`

#### Request headers

- `X-Gestiona-Addon-Token` required; value from `Gestiona:AddonToken`

#### Request body model

- Empty byte array

#### Response data used

- `Location` header. The final URL segment is returned by the gateway as `authId`.
- A missing or invalid `Location` header is treated as an upstream failure.

### 32. GET `/addon/authorizations/{auth_id}`

Checks whether an add-on authorization is pending or authorized.

#### Used by

- `GET /addon/authorizations/{auth_id}`

#### Request headers

- `X-Gestiona-Addon-Token` required; value from `Gestiona:AddonToken`

#### Response data used

- HTTP `401`: the authorization is pending. The `Location` header is returned as `authorizeUrl`.
- HTTP `200`: the authorization is complete. Body fields `user_id` and `access_token` are returned as `authorizedInfo.userId` and `authorizedInfo.accessToken`.
- Other non-success status codes are propagated as gateway errors.
