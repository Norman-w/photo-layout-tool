# 照片拼版工具

Vue 3 + .NET 9 API，支持上传、抠图换底与画布拼版。

## 本地开发

```bash
chmod +x dev.sh deploy.sh
./dev.sh
```

- 前端：http://localhost:5173  
- API：http://127.0.0.1:5066  

## 发布（145 服务器）

```bash
./deploy.sh
# 默认 root@192.168.7.145 → /opt/photo-layout，端口 5088
```

公网入口需在 **192.168.16.2** Nginx 加入 `deploy/nginx.norman.wang.snippet.conf`：

- https://norman.wang/tools/photo-layout/

个人站工具专栏入口：`/tools` → 照片拼版。
