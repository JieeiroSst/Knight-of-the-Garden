# Backend Hiep Si Ve Vuon

Backend luu tru game (Node.js/Express + PostgreSQL) - thay the hoan toan viec luu file JSON local
truoc day. Game (Godot) CAN backend nay dang chay moi choi/dang nhap/luu duoc - khong co che do
choi offline.

## Chay thu tren may cua ban (local, de kiem tra)

```
cd backend
docker compose up -d --build
curl http://localhost:3000/health   # {"ok":true}
```

Postgres + API se tu khoi dong, tu tao bang (`db/schema.sql`) trong lan chay dau tien. Game Godot
mac dinh tro ve `http://localhost:3000` (xem `BackendClient.BaseUrl` trong Inspector cua node
autoload `BackendClient`, hoac sua truc tiep trong `scripts/systems/BackendClient.cs`).

Dung stack: `docker compose down` (them `-v` neu muon xoa luon du lieu Postgres da luu).

## Deploy that su len Internet (de choi tu xa)

Chon 1 trong cac lua chon sau, roi cap nhat `BackendClient.BaseUrl` trong Godot thanh URL cong
khai nhan duoc:

### Option A - Render / Railway / Fly.io (de nhat, co goi mien phi)

1. Push thu muc `backend/` len 1 repo Git (co the la 1 repo rieng, hoac 1 thu muc con trong repo
   game hien tai).
2. Tren Render/Railway/Fly.io: tao 1 "Web Service" moi tro toi thu muc `backend/`, chon build
   bang Dockerfile co san (`backend/Dockerfile`).
3. Tao 1 PostgreSQL database tren cung nen tang do (Render/Railway deu co Postgres mien phi/gia
   re dang add-on).
4. Khai bao bien moi truong cho service (KHONG commit `.env` that len git):
   - `DATABASE_URL` = connection string Postgres ma nen tang cung cap
   - `JWT_SECRET` = 1 chuoi bi mat dai, ngau nhien (vd tao bang `openssl rand -hex 32`)
   - `JWT_EXPIRES_IN` = `30d` (hoac tuy chinh)
   - `PORT` = nen tang thuong tu dong gan, co the bo qua
5. Deploy xong se co 1 URL cong khai dang `https://ten-app.onrender.com` (hoac tuong tu) - dan
   URL nay vao `BackendClient.BaseUrl`.

### Option B - VPS rieng (vd DigitalOcean, Vultr, 1 may chu Linux bat ky)

1. Cai Docker + Docker Compose tren VPS.
2. Copy thu muc `backend/` len VPS (`git clone` hoac `scp`).
3. Tao file `.env` that tren VPS tu `.env.example`, dien `JWT_SECRET` ngau nhien rieng (KHONG
   dung gia tri mac dinh trong vi du).
4. `docker compose up -d --build` tren VPS.
5. Mo cong 3000 tren firewall (hoac dat 1 reverse proxy nhu Nginx/Caddy truoc de dung HTTPS that
   su qua cong 443 - khuyen nghi cho du lieu dang nhap that).
6. Dan `http://<ip-vps>:3000` (hoac `https://ten-mien-cua-ban`) vao `BackendClient.BaseUrl`.

## Luu y bao mat khi deploy that

- **Bat buoc doi `JWT_SECRET`** khoi gia tri mau trong `.env.example`/`docker-compose.yml` truoc
  khi dua len Internet that.
- Nen dat HTTPS truoc backend (qua nen tang PaaS o Option A da co san, hoac tu cau hinh Nginx/
  Caddy + Let's Encrypt o Option B) vi username/password dang gui qua request dang ky/dang nhap.
- Khong commit file `.env` that (chi `.env.example`) len git.
