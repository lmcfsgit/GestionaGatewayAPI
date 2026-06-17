# Gestiona Gateway API

<center>_Version 1.1.1_</center>

### Features

- Middleware for logging request client information
- New endpoints:
  - GET .../processes/{process_id}/thirds
  - GET .../processes/thirds?process_number=<proces_number value>
  - GET .../thirds/{third_id}
  - GET .../thirds?nif=<nif>

### Bug Fixs

### Improvements

- The log of the request body doesn't print the Content anymore, it only informs that it's present.

<center>_Version 1.2.1_</center>

### Features

- Support for X-User-Access-Token header.
  If it's present uses this token for the X-Gestiona-Access-Token else use the environment variable Gestiona\_\_AccessToken

### Bug Fixs

### Improvements

- Added swagger support to the API: .../swagger

<center>_Version 1.3.1_</center>

### Features

- New endpoint for resolving process id form process number: .../processes?process_number=<process number>

### Bug Fixs

### Improvements

- Added log debug information for the request body sent to Gestiona API
- Api version is read from .csproj instead of appsettings.json
- Added Zone (Concelho) to third address
- Added ParishCode obtained from last href segment of the link where ref="parish"
- Added second_surname from Gestiona to the third model

<center>_Version 1.?.?_</center>

### Features

### Bug Fixs

### Improvements
