# Cấu hình client và thư mục dữ liệu game (MU pack)

Tài liệu này mô tả cách chỉnh **config**, **đường dẫn asset MU (~1.7 GB)**, và **lần đầu tải/giải nén** Full pack để lần sau không phải tải lại khi đổi branch hay build lại. Cuối doc có thêm phần **đổi WiFi / đổi mạng** để lần sau chỉ sửa đúng chỗ (server vs local data).

---

## File cấu hình được load như thế nào

1. **`appsettings.json`** nằm cạnh file chạy (output của project head, ví dụ `MuMac/bin/...`), không đọc theo thư mục làm việc hiện tại của terminal.
2. **`appsettings.local.json`** (tùy chọn, nên đặt cạnh exe và **không commit** — đã có trong `.gitignore`) được merge **đè** lên giá trị trong `appsettings.json`. Dùng cho máy bạn: IP server LAN, đường dẫn data riêng, v.v.
3. Section chính: **`MuOnlineSettings`** trong JSON map vào class `Client.Main.Configuration.MuOnlineSettings`.

Sau khi load xong, client log dòng kiểu:

- `✅ Configuration loaded.`
- `✅ Game data directory: <đường dẫn tuyệt đối>`

Nếu không thấy đường dẫn data đúng ý, kiểm tra `GameDataDirectory` và file local override.

---

## Các khóa quan trọng trong `MuOnlineSettings`

| Khóa | Ý nghĩa |
|------|---------|
| `ConnectServerHost` | Host Connect Server (OpenMU thường `127.0.0.1` khi chạy Docker trên máy). |
| `ConnectServerPort` | Cổng Connect Server (stack all-in-one OpenMU thường **44405**). |
| `ProtocolVersion` | Ví dụ `Season6` — phải khớp server. |
| `ClientVersion` / `ClientSerial` | Chuỗi client — phải khớp cấu hình server/OpenMU. |
| `GameDataDirectory` | Thư mục chứa **đã giải nén** MU Full pack (`Gate.bmd`, `Local`, …). **Để trống** thì dùng thư mục cố định theo OS (xem dưới). |

Graphics, `DirectionMap`, `PacketLogging`, `Environment`… chỉnh trong cùng section nếu cần.

---

## Thư mục data game (`GameDataDirectory`)

Logic resolve nằm trong `Client.Main/Configuration/GameDataPathResolver.cs`.

- **`GameDataDirectory` có giá trị (không rỗng)**  
  Đường dẫn **tuyệt đối** tới thư mục gốc data (nơi có `Gate.bmd`). Hỗ trợ `~/...` trên macOS/Linux và biến môi trường.

- **`GameDataDirectory` để `""` hoặc bỏ khóa**  
  Client dùng thư mục cố định **ngoài repo**, không phụ thuộc `bin/`:

  - **macOS:** `~/Library/Application Support/MUMono/Data`
  - **Linux:** `~/.local/share/MUMono/Data`
  - **Windows:** `%LocalAppData%\MUMono\Data`

**Mục đích:** đổi branch hay clean build **không xóa** ~1.7 GB asset; chỉ cần một lần tải/giải nén xong vào thư mục trên (hoặc vào path bạn chỉ định).

### Ví dụ `appsettings.local.json` (máy dev)

```json
{
  "MuOnlineSettings": {
    "ConnectServerHost": "192.168.1.10",
    "ConnectServerPort": 44405,
    "GameDataDirectory": "/Volumes/Games/MU_Red/Data"
  }
}
```

---

## Lần đầu: tải Full pack và giải nén

1. Khi **`Gate.bmd`** chưa có trong `DataPath` đã resolve (và không có file/thư mục “đã extract” khác), `LoadScene` sẽ **tải ZIP** từ URL mặc định trong code (`Constants.DefaultDataPathUrl` — bản WebZen Full) vào `\<DataPath\>/Data.zip`, rồi **giải nén** vào cùng `DataPath`.
2. Sau khi giải nén thành công, `Data.zip` thường được xóa (cleanup).
3. Lần chạy sau, nếu phát hiện đã có asset (ưu tiên có **`Gate.bmd`**), client **bỏ qua download** (status kiểu “Assets found – skipping download.”).

**Lưu ý:**

- Để **không tải lại**, cần để lần đầu **chạy xong** quá trình Download + Extract; thoát giữa chừng có thể chỉ còn `Data.zip` hoặc extract dở → lần sau có thể **tải lại**.
- Debug console có dòng: `[LoadScene] DataPath=..., assetsReady=True|False` để biết có nhận asset hay không.

---

## Chuyển data từ thư mục build cũ (`bin/.../Data`)

Nếu trước đây bạn đã giải nén vào output project (ví dụ `MuMac/bin/Debug/net10.0/osx-arm64/Data`), có thể **copy** toàn bộ nội dung sang thư mục persistent (macOS ví dụ):

```bash
rsync -a --progress "/path/to/MuMac/bin/Debug/net10.0/osx-arm64/Data/" \
  "$HOME/Library/Application Support/MUMono/Data/"
```

Sau đó khởi động lại client; nếu `Gate.bmd` đã có đúng chỗ, sẽ không tải lại ~1.7 GB.

---

## OpenMU và địa chỉ Game Server (`127.127.127.127`)

Connect Server có thể trả về địa chỉ game server kiểu **`127.127.127.127`** (loopback alias trong OpenMU cho máy local). Client **chuẩn hóa sang `127.0.0.1`** khi mở TCP tới Game Server để kết nối ổn định; không cần chỉnh tay trong JSON cho việc này.

Nếu không vào được game server: kiểm tra firewall, Docker publish cổng game server (thường trong log là port kiểu **55901**), và OpenMU đang chạy.

---

## Setup một lần để sau đổi WiFi / đổi mạng ít phải sửa

### Hai thứ tách biệt

| Thứ | Đổi WiFi có ảnh hưởng? | Ghi nhớ |
|-----|-------------------------|---------|
| **MU Full pack (~1.7 GB)** trên ổ máy | Không — path cố định (`GameDataDirectory` hoặc `Application Support/.../MUMono/Data`) | Chỉ cần giải nén đúng một lần; đổi mạng không xóa folder này. |
| **Địa chỉ Connect Server / Game Server** | Có khi server **không** chạy trên máy bạn | IP LAN máy chạy Docker/OpenMU có thể đổi (DHCP). |

### Cách cài đặt “đỡ đau đầu” lần sau

1. **Luôn đặt IP/port máy chủ trong `appsettings.local.json`** (cạnh exe), không hard-code IP LAN vào `appsettings.json` trong repo. Repo không chứa IP nhà bạn; máy khác clone vẫn chạy với default `127.0.0.1` nếu cần.
2. **`GameDataDirectory`** để trống hoặc trỏ một thư mục ngoài repo (ổ ngoài, `~/Games/...`) — một lần download/extract xong thì **đổi WiFi không làm tải lại pack**.
3. Trong game, nếu có chỉnh Connect Server và client **ghi file local** (merge vào `appsettings.local.json`), lần sau mở game vẫn nhớ host/port đã lưu.

### Khi đổi WiFi hoặc sang mạng khác — làm gì?

**Trường hợp A — OpenMU chạy trên chính máy đang chơi (Docker local)**

- `ConnectServerHost` thường vẫn **`127.0.0.1`**, cổng **`44405`**. Đổi WiFi **không cần** đổi config client cho Connect Server.
- Nếu không vào được: kiểm tra Docker/OpenMU có đang chạy, cổng có bind đúng không (firewall macOS cho `dotnet`/container).

**Trường hợp B — Server chạy máy khác (PC/NAS/Raspberry) trên LAN**

- Sau khi đổi WiFi, laptop có thể vào **subnet hoặc segment khác**; địa chỉ **IP của máy server** cũng có thể đổi nếu router cấp DHCP mới.
- Làm lần lượt:
  1. Trên máy server (hoặc router admin): xem IP hiện tại của máy đó (ví dụ `192.168.x.y`).
  2. Từ máy client: `ping <IP>` và kiểm tra cổng Connect Server mở được không, ví dụ `nc -vz <IP> 44405` (hoặc `telnet`).
  3. Sửa **`appsettings.local.json`**: `ConnectServerHost` = IP hoặc hostname LAN mới; `ConnectServerPort` giữ **`44405`** nếu không đổi deploy.
  4. Chạy lại client.

**Trường hợp C — Muốn ít đổi IP nhất có thể**

- Trên router: **DHCP reservation** (gán IP cố định theo MAC) cho máy chạy OpenMU.
- Hoặc dùng **tên máy / mDNS** (`.local`) nếu mạng của bạn resolve được và Connect Server lắng nghe đúng interface.

### Không cần làm lại khi đổi WiFi

- Giải nén **MU Full pack** (miễn là folder `GameDataDirectory` không xóa).
- **Branch Git**, build lại project — miễn là `GameDataDirectory` vẫn trỏ đúng (hoặc default persistent folder).

### Checklist nhanh sau khi “mất kết nối” sau đổi mạng

1. Server OpenMU/Docker có đang chạy không?
2. IP/port trong `appsettings.local.json` còn đúng máy chủ không?
3. Firewall/router có chặn LAN-to-LAN hoặc client isolation trên WiFi khách?
4. Log client: lỗi timeout Connect Server → gần như chỉ host/port/route LAN.

---

## Liên quan file trong repo

| File | Vai trò |
|------|---------|
| `Client.Main/appsettings.json` | Giá trị mặc định commit được (không chứa secret/path máy bạn). |
| `Client.Main/MuGame.cs` | Load JSON, set `Constants.DataPath` qua `GameDataPathResolver`. |
| `Client.Main/Configuration/MuOnlineSettings.cs` | Model `MuOnlineSettings`. |
| `Client.Main/Scenes/LoadScene.cs` | Download / extract ZIP, kiểm tra `Gate.bmd`. |
| `Client.Main/Networking/NetworkManager.cs` | Chuẩn hóa host game server loopback. |

---

## MuMac và content pipeline

Build macOS (`MuMac/MuMac.csproj`) có thể dùng **content `.xnb` build sẵn** (DesktopGL) thay vì chạy MGCB trên máy — khác với **MU Full client data** (~1.7 GB) ở `GameDataDirectory`. Hai loại path không lẫn nhau.
