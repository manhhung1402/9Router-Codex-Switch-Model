# 9Router · Codex · Đổi mô hình

Công cụ cửa sổ cho Windows giúp xem và chuyển mô hình của 9Router sang Codex.

## Mô tả

9Router là bộ trung gian định tuyến request tới nhiều nhà cung cấp AI. Mỗi lần đổi mô hình
trên web UI của 9Router, Codex vẫn giữ mô hình cũ trong `config.toml` — hai bên lệch nhau.

Tool này đọc danh sách mô hình trực tiếp từ 9Router, cho bạn lọc và chọn, rồi ghi đúng giá trị
vào `config.toml` của Codex.

Chức năng:

- Lọc mô hình theo ký tự, tô đậm phần ký tự khớp
- Ghi `model` vào khối gốc `config.toml`, tự tạo bản sao lưu trước khi ghi
- Quản lý bản sao lưu: xem, khôi phục, xoá
- Tự đọc API key từ cơ sở dữ liệu 9Router và khai báo provider
- Ngắt 9Router, đưa Codex về nhà cung cấp mặc định của OpenAI mà không mất cấu hình khác

## Cài đặt

Yêu cầu: Windows 10/11. Không cần cài thêm gì — .NET Framework đã có sẵn trên Windows.

Tải `9Router - Codex - Đổi model.exe`, copy sang máy và nhấp đúp để chạy.

### Máy mới

1. Mở Codex Desktop App một lần để nó tạo `config.toml`
2. Cài và bật 9Router trên cùng máy đó
3. Mở tool, bấm **Kết nối 9Router** để tự nhận API key và ghi provider
4. Bấm **Làm mới** để danh sách mô hình hiện ra

### Dùng ngay trên máy đang chạy

Nếu 9Router và Codex đã kết nối rồi, bỏ qua bước trên — mở tool và chọn mô hình là được.

## Build từ mã nguồn

```
build.cmd
```

Chạy trực tiếp, không cần Visual Studio. Script tự gọi `csc.exe` của .NET Framework rồi ghi
file `.exe` ra thư mục cha.
