# Instalação no IIS

Este guia descreve como instalar ou atualizar um pacote publicado da Gestiona
Gateway API como um site do IIS, utilizando o `Install-IIS.ps1`.

## Requisitos

- Uma instalação suportada do Windows ou Windows Server com o IIS ativado.
- A funcionalidade Scripts e Ferramentas de Gestão do IIS, que disponibiliza o
  módulo `WebAdministration` do PowerShell.
- O Pacote de Alojamento do .NET 8, incluindo o ASP.NET Core Module V2. Reinicie
  o IIS depois de instalar o pacote.
- Windows PowerShell executado como Administrador.
- Um ficheiro ZIP com a aplicação publicada. Deve conter exatamente um
  `GestionaGatewayAPI.dll` e um `web.config` no diretório da aplicação publicada.
- Uma porta HTTP livre, um nome de site do IIS exclusivo e um diretório de
  instalação que não exista ou esteja vazio.
- Acesso de rede do servidor ao endpoint configurado da API Gestiona.

O SDK do .NET só é necessário quando o pacote é produzido no servidor. Não é
necessário para executar uma aplicação implementada com o Pacote de Alojamento
do .NET 8.

## Instalação

Abra o Windows PowerShell como Administrador, mude para o diretório que contém
o instalador e execute:

```powershell
.\Install-IIS.ps1 `
    -ZipPath C:\publish\GestionaGatewayAPI.zip `
    -Port 8080 `
    -InstallPath C:\inetpub\GestionaGatewayAPI `
    -SiteName GestionaGatewayAPI
```

O `SiteName` é opcional e, por predefinição, utiliza `GestionaGatewayAPI`.

| Parâmetro | Obrigatório | Descrição |
| --- | --- | --- |
| `ZipPath` | Sim | Caminho para o ficheiro ZIP da aplicação publicada. |
| `Operation` | Não | `Install` (a predefinição) ou `Update`. |
| `Port` | Apenas instalação | Porta HTTP entre 1 e 65535. Não pode estar a ser utilizada por uma ligação do IIS. |
| `InstallPath` | Apenas instalação | Destino dos ficheiros da aplicação. Se já existir, deve estar vazio e não pode ser a raiz de uma unidade. |
| `SiteName` | Não | Nome do site do IIS. Por predefinição, utiliza `GestionaGatewayAPI`; um novo conjunto aplicacional utiliza o mesmo nome. |

O script efetua as seguintes operações:

1. Valida o ficheiro ZIP, o módulo do IIS, o ASP.NET Core Module V2, a porta, o
   nome do site e o destino.
2. Extrai e copia a aplicação publicada.
3. Cria um diretório `logs`.
4. Cria um conjunto aplicacional do IIS utilizando `ApplicationPoolIdentity` e
   sem CLR gerido.
5. Concede ao conjunto aplicacional acesso de leitura à aplicação e acesso de
   modificação a `logs`.
6. Cria e inicia um site HTTP associado a todos os endereços IP locais na porta
   solicitada.

Se uma nova instalação falhar, o script remove o site, o conjunto aplicacional e
os ficheiros criados por essa tentativa de instalação. O script recusa-se a
substituir um site, conjunto aplicacional ou ligação de porta do IIS não
relacionado, bem como um caminho de instalação que não esteja vazio.

Para instalar outra instância em paralelo com a primeira, utilize um nome de
site, uma porta e um caminho de instalação diferentes:

```powershell
.\Install-IIS.ps1 `
    -ZipPath C:\publish\GestionaGatewayAPI.zip `
    -Port 8081 `
    -InstallPath C:\inetpub\GestionaGatewayAPI-Test `
    -SiteName GestionaGatewayAPI-Test
```

## Atualização

Selecione a operação de atualização e indique o novo pacote. Especifique
`SiteName` apenas quando o site existente não utilizar o nome predefinido:

```powershell
.\Install-IIS.ps1 `
    -Operation Update `
    -ZipPath C:\publish\GestionaGatewayAPI.zip `
    -SiteName GestionaGatewayAPI
```

O instalador obtém o caminho físico, o conjunto aplicacional atribuído e a porta
HTTP a partir do site existente no IIS. Uma atualização só é permitida quando o
site indicado, o conjunto aplicacional atribuído, o caminho físico e uma ligação
HTTP existem.

Antes de substituir os ficheiros, o instalador valida o novo pacote e copia a
aplicação atual para uma cópia de segurança temporária. Durante a atualização:

1. Regista se o site e o conjunto aplicacional estão em execução.
2. Para o site e o conjunto aplicacional, caso estejam em execução.
3. Substitui os ficheiros da aplicação instalada.
4. Preserva o diretório `logs`, os ficheiros `appsettings*.json` existentes na
   raiz e o `web.config`.
5. Restaura o estado anterior, em execução ou parado.

Se a substituição falhar, os ficheiros anteriores da aplicação e o estado do
serviço são restaurados automaticamente. Os diretórios temporários do pacote e
da cópia de segurança são removidos depois da conclusão.

## Configuração

Configure o `appsettings.json` instalado antes de iniciar o tráfego de produção
ou disponibilize variáveis de ambiente equivalentes. Nos nomes das variáveis de
ambiente do ASP.NET Core, utilizam-se dois caracteres de sublinhado (`__`) em
substituição dos dois pontos (`:`) da configuração.

| Definição | Variável de ambiente | Descrição |
| --- | --- | --- |
| `Gestiona:GestionaApiBaseUrl` | `Gestiona__GestionaApiBaseUrl` | URL base da API REST Gestiona. |
| `Gestiona:AccessToken` | `Gestiona__AccessToken` | Token de acesso predefinido da Gestiona. Este valor deve ser definido como uma variável de ambiente do sistema e não pode ser guardado no `appsettings.json`. Os pedidos que suportam `X-User-Access-Token` utilizam esse cabeçalho em vez deste valor. |
| `Gestiona:AddonToken` | `Gestiona__AddonToken` | Token utilizado nas operações de autorização de add-ons. |
| `Serilog:MinimumLevel:Default` | `Serilog__MinimumLevel__Default` | Nível mínimo de registo da aplicação, como `Information` ou `Debug`. |

Exemplo de configuração:

```json
{
  "Gestiona": {
    "GestionaApiBaseUrl": "https://gestiona.example/rest",
    "AddonToken": "replace-with-a-secret"
  }
}
```

Defina o token de acesso como uma variável de ambiente do Windows ao nível da
máquina, a partir de uma sessão elevada do PowerShell:

```powershell
[Environment]::SetEnvironmentVariable(
    "Gestiona__AccessToken",
    "replace-with-a-secret",
    [EnvironmentVariableTarget]::Machine)
```

Não adicione `Gestiona:AccessToken` ao `appsettings.json` nem submeta tokens de
produção ao controlo de versões. Depois de criar ou alterar a variável de
ambiente do sistema, reinicie o IIS para que os processos de trabalho recebam o
novo valor:

```powershell
iisreset
```

Depois de alterar outra configuração da aplicação, recicle o conjunto
aplicacional:

```powershell
Restart-WebAppPool -Name GestionaGatewayAPI
```

O Serilog grava ficheiros contínuos em `<InstallPath>\logs`. Definir o nível
mínimo como `Debug` pode registar detalhes dos pedidos e só deve ser ativado
quando necessário.

## Verificar a instalação

Verifique o site e o conjunto aplicacional do IIS:

```powershell
Get-Website -Name GestionaGatewayAPI
Get-WebAppPoolState -Name GestionaGatewayAPI
```

Quando termina, o instalador apresenta a ligação local, por exemplo:

```text
http://localhost:8080
```

Consulte `<InstallPath>\logs` se a aplicação não iniciar. A interface do Swagger
só é ativada quando a aplicação é executada no ambiente `Development`.

O instalador cria apenas uma ligação HTTP. Configure separadamente o certificado
do servidor e uma ligação HTTPS do IIS quando for necessário utilizar TLS.
