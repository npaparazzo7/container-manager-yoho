# ContainerManager

REST API for managing LXC containers with NVIDIA GPU MIG slice assignment. Built with C# and .NET 10 on ASP.NET Core.

## Description

ContainerManager exposes a REST API that allows you to:

- Detect available MIG slices on the NVIDIA GPU
- Create LXC containers and assign them a specific MIG slice
- Monitor the status of active containers
- Delete containers and automatically release the associated MIG slice
- Automatically synchronize state on startup (reboot handling)

Every request is authenticated via **HMAC-SHA256** and filtered by a **runtime-configurable IP whitelist**.

## Requirements

### Development environment
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022+ or VS Code with C# extension

### Production environment (Linux)
- Ubuntu 22.04 / 24.04
- [LXD](https://documentation.ubuntu.com/lxd/en/latest/) installed and configured
- NVIDIA GPU with MIG support
- NVIDIA drivers with `nvidia-smi` available
- .NET 10 Runtime

## Installation

### Clone the repository

```bash
git clone https://github.com/your-username/ContainerManager.git
cd ContainerManager
```

### Restore dependencies

```bash
dotnet restore
```

### Install Scalar (API UI)

```bash
dotnet add package Scalar.AspNetCore
```

### Run in development mode (mock)

```bash
dotnet run
```

The API will be available at `http://localhost:5000`.
The Scalar UI will be available at `http://localhost:5000/scalar/v1`.

---

## Configuration

### `appsettings.json`

```json
{
  "AppSettings": {
    "HmacSecretKey": "replace-this-with-a-secure-key",
    "IpWhitelistPath": "ip-whitelist.json",
    "UseMock": true
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=containerManager.db"
  },
  "Urls": "http://0.0.0.0:5000"
}
```

| Parameter | Description |
|---|---|
| `HmacSecretKey` | Shared secret key used for HMAC signing |
| `IpWhitelistPath` | Path to the JSON file containing allowed IPs |
| `UseMock` | `true` to use mock services, `false` for real services |

### `ip-whitelist.json`

```json
{
  "AllowedIPs": [
    "127.0.0.1",
    "192.168.1.100"
  ]
}
```

The `ip-whitelist.json` file is monitored in real time — any change is applied **without restarting the application**.

## Authentication

Every request must include the following HTTP headers:

| Header | Description |
|---|---|
| `X-Timestamp` | Unix timestamp in seconds (UTC) |
| `X-Signature` | HMAC-SHA256 signature in lowercase hex format |

### Signature calculation

```
payload = "{METHOD}\n{PATH}\n{TIMESTAMP}\n{BODY}"
signature = HMAC-SHA256(payload, secret_key) → lowercase hex
```

**Example:**

```
payload = "POST\n/api/containers\n1743000000\n{\"nome\":\"test\",\"migUuid\":\"MIG-...\"}"
signature = a3f8c2d1...
```

> The accepted time window is **±30 seconds**. Requests with older timestamps are rejected to prevent replay attacks.

## API Endpoints

### MIG Slices

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/mig` | List all MIG slices detected on the GPU |
| `GET` | `/api/mig/liberi` | List MIG slices not assigned to any container |

### Containers

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/containers` | List all containers (excluding DELETED) |
| `GET` | `/api/containers/attivi` | List only RUNNING containers |
| `GET` | `/api/containers/{nome}` | Get details of a single container |
| `POST` | `/api/containers` | Create a new container |
| `DELETE` | `/api/containers/{nome}` | Delete a container |

## Linux Deployment

### 1. Publish the application

```bash
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish
```

### 2. Copy files to the server

```bash
scp -r ./publish/* user@server:/opt/containermanager/
```

### 3. Configure permissions

```bash
# Make the binary executable
chmod +x /opt/containermanager/ContainerManager

# Recommended: dedicated user with limited sudo access
useradd -r -s /bin/false containermgr
visudo -f /etc/sudoers.d/containermgr
```

Sudoers file content:
```
containermgr ALL=(ALL) NOPASSWD: /usr/bin/lxc, /usr/bin/nvidia-smi
```

### 4. Configure the systemd service

Create the file `/etc/systemd/system/containermanager.service`:

```ini
[Unit]
Description=Container Manager API
After=network.target

[Service]
User=containermgr
WorkingDirectory=/opt/containermanager
ExecStart=/opt/containermanager/ContainerManager
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

### 5. Start the service

```bash
systemctl daemon-reload
systemctl enable containermanager
systemctl start containermanager
systemctl status containermanager
```

### 6. Set `UseMock: false` in `appsettings.json`

```json
"AppSettings": {
    "UseMock": false
}
```

## Database

The SQLite database is created automatically on first startup in the application working directory.

### `containers` table

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER | Primary key |
| `nome` | TEXT | Unique container name |
| `stato` | TEXT | `RUNNING`, `STOPPED`, `DELETED` |
| `mig_uuid` | TEXT | UUID of the assigned MIG slice |
| `mig_device_index` | INTEGER | Device index (stable across reboots) |
| `mig_tipo` | TEXT | MIG type (e.g. `1g.6gb`, `2g.12gb`) |
| `vram_gb` | REAL | VRAM in GB |
| `data_creazione` | TEXT | Creation date (ISO 8601) |
| `data_modifica` | TEXT | Last modified date (ISO 8601) |

> Deleted containers are not physically removed from the database but marked as `DELETED` to preserve history.


---

## License

MIT
