# Distributed Gomoku Server Architecture

## Tổng quan

Dự án đã được mở rộng từ mô hình server đơn thành **mô hình phân tán** với các thành phần sau:

### Kiến trúc hệ thống

```
Client ← → MainServer (Load Balancer) ← → WorkerServer (AI + Logic Processing)
```

## Các thành phần chính

### 1. **MainServer** (Coordinator + Load Balancer)
- **Port**: 5000 (mặc định)
- **Chức năng**: 
  - Tiếp nhận kết nối từ client
  - Matchmaking và quản lý phòng game
  - Monitor load hệ thống
  - Quyết định khi nào delegate công việc cho WorkerServer
  - Xử lý logic cục bộ khi load thấp

### 2. **WorkerServer** (Compute Node)
- **Port**: 6000 (mặc định) 
- **Chức năng**:
  - Xử lý AI Gomoku với thuật toán Minimax + Alpha-Beta pruning
  - Validate di chuyển phức tạp
  - Phân tích game logic khi server quá tải

### 3. **SharedLib** (Common Library)
- **GameEngine**: Logic game Gomoku, kiểm tra thắng/thua
- **AI**: Bot AI thông minh với thuật toán tối ưu
- **Models**: PlayerInfo, MoveData, các data models chung
- **Communication**: Protocol giao tiếp giữa MainServer và WorkerServer

## Load Balancing Logic

### Ngưỡng Load:
- **Low Load** (< 50%): Xử lý tất cả tại MainServer
- **Medium Load** (50-80%): Delegate AI moves cho WorkerServer  
- **High Load** (> 80%): Delegate cả AI và move validation cho WorkerServer

### Các yếu tố đánh giá load:
- CPU usage của hệ thống
- Số lượng game đang diễn ra
- Memory pressure
- Số request đang xử lý

## Cách chạy hệ thống

### Chạy riêng lẻ:

1. **Khởi động WorkerServer** (Terminal 1):
```bash
cd WorkerServer
dotnet run
```

2. **Khởi động MainServer** (Terminal 2): 
```bash
cd MainServer  
dotnet run
```

### Chạy toàn bộ hệ thống:
```bash
cd MainServer
dotnet run
# Chọn option 3 để start cả MainServer và WorkerServer
```

## Communication Protocol

### MainServer → WorkerServer:

#### AI Move Request:
```json
{
  "RequestId": "guid",
  "Type": "AI_MOVE_REQUEST", 
  "Data": {
    "Board": "15x15 array",
    "AISymbol": "X|O",
    "RoomId": "room_id"
  }
}
```

#### Move Validation Request:
```json
{
  "RequestId": "guid",
  "Type": "VALIDATE_MOVE_REQUEST",
  "Data": {
    "Board": "15x15 array", 
    "Row": 7,
    "Col": 7,
    "PlayerSymbol": "X|O"
  }
}
```

### WorkerServer → MainServer:

#### AI Move Response:
```json
{
  "RequestId": "guid",
  "Type": "AI_MOVE_RESPONSE",
  "Status": "SUCCESS|ERROR", 
  "Data": {
    "Row": 7,
    "Col": 7,
    "IsValid": true
  }
}
```

## Tính năng mới

### 1. **AI Gomoku thông minh**
- Thuật toán Minimax với Alpha-Beta pruning
- Depth = 4 moves lookahead
- Pattern recognition cho các tình huống chiến thuật
- Tốc độ tối ưu với early termination

### 2. **Load Balancing tự động**
- Real-time monitoring system load
- Dynamic task distribution dựa trên threshold
- Graceful fallback khi WorkerServer unavailable
- Health check tự động cho workers

### 3. **Fault Tolerance**
- Auto-reconnect khi WorkerServer disconnect
- Fallback xử lý local khi không có worker
- Request timeout và retry mechanism
- Graceful degradation

### 4. **Scalability**
- Có thể thêm nhiều WorkerServer nodes
- Round-robin load distribution
- Horizontal scaling support
- Configurable worker endpoints

## Cấu hình nâng cao

### Thêm WorkerServer nodes:
```csharp
// Trong DistributedMainServer.StartAsync()
await workerManager.AddWorkerAsync("worker1.example.com", 6000);
await workerManager.AddWorkerAsync("worker2.example.com", 6001); 
await workerManager.AddWorkerAsync("localhost", 6002);
```

### Điều chỉnh Load Thresholds:
```csharp
// Trong LoadBalancer.cs
private const int HIGH_LOAD_THRESHOLD = 80; // Có thể thay đổi
private const int MEDIUM_LOAD_THRESHOLD = 50;
private const int MAX_CONCURRENT_GAMES = 100;
```

## Monitoring & Debugging

### Load Stats được hiển thị mỗi 30 giây:
```
Load Stats - System: 45%, Games: 23%, Level: Medium
```

### Logs quan trọng:
- Worker connection/disconnection
- Request delegation decisions  
- Health check status
- Performance metrics

## Migration từ Legacy

Hệ thống vẫn bảo tồn:
- **LegacyProgram.cs**: Code server cũ để tham khảo
- **Backward compatibility**: Client cũ vẫn hoạt động
- **Gradual migration**: Có thể chuyển từng tính năng

## Kế hoạch mở rộng tương lai

1. **Database integration** cho persistent game state
2. **Web dashboard** để monitor cluster
3. **Auto-scaling** dựa trên metrics
4. **Machine Learning** để tối ưu AI
5. **Multi-region deployment** 

## Troubleshooting

### Lỗi thường gặp:

1. **WorkerServer connection failed**
   - Kiểm tra WorkerServer đã start chưa
   - Verify port không bị block bởi firewall

2. **High latency** 
   - Check network connection giữa MainServer và WorkerServer
   - Monitor system resources

3. **Build errors**
   - Ensure tất cả dependencies đã restore: `dotnet restore`
   - Check .NET 8.0 SDK installed

Hệ thống distributed này cung cấp **high availability**, **better performance** và **horizontal scalability** cho game Gomoku server!