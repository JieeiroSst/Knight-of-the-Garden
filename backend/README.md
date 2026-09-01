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

Postgres + API se tu khoi dong, tu chay migration (`db/migrations/`) trong lan chay dau tien. Game
Godot mac dinh tro ve `http://localhost:3000` (xem `BackendClient.BaseUrl` trong Inspector cua node
autoload `BackendClient`, hoac sua truc tiep trong `scripts/systems/BackendClient.cs`).

Dung stack: `docker compose down` (them `-v` neu muon xoa luon du lieu Postgres da luu).

## Kien truc code

Backend duoc to chuc theo LOP (thay vi vai file phang nhu ban dau) - moi lop chi phu thuoc lop
duoi no, KHONG di nguoc:

```
routes/        -> gan URL toi controller (+ middleware validate/auth/rate-limit rieng tung route)
controllers/    -> lop HTTP mong: doc req, goi service, dinh dang response - KHONG chua nghiep vu
services/       -> logic nghiep vu THAT (bam mat khau, ky JWT, quyet dinh 404/409/401...)
repositories/   -> CHI noi viet SQL - service khong bao gio tu query truc tiep
middleware/     -> auth (JWT), validate (zod), rate limit, log request, xu ly loi tap trung
config/env.js   -> doc + KIEM TRA moi bien moi truong 1 lan luc khoi dong (fail-fast neu sai)
utils/          -> logger (pino, JSON co cau truc), AppError, asyncHandler, jwt
db/pool.js      -> ket noi Postgres (co gioi han so ket noi - xem phan quy mo o duoi)
db/migrate.js   -> chay db/migrations/*.sql THEO THU TU, moi file CHI 1 lan (bang schema_migrations)
```

`app.js` lap rap toan bo (khong tu goi `listen()`) de `tests/` dung THANG duoc app that ma khong
can mo cong that; `index.js` chi lo khoi dong/tat mem.

**API duoc mo ta day du trong [`openapi.yaml`](openapi.yaml)** (dan vao https://editor.swagger.io
hoac `npx @redocly/cli preview-docs openapi.yaml` de xem dang trang web).

**Test**: `npm install && npm test` (can Postgres dang chay va truy cap duoc qua `DATABASE_URL` -
mac dinh trong lenh duoi day tro ve Postgres cua `docker compose`):

```
DATABASE_URL=postgres://hsvv:hsvv_password@localhost:5432/hsvv \
JWT_SECRET=test-secret-du-16-ky-tu-tro-len \
npm test
```

Bo test gom unit test cho validation (zod, khong can DB) va integration test THAT (dang ky/dang
nhap/luu/nap that qua HTTP + Postgres that, khong mock).

**URL/JSON response KHONG DOI** so voi ban dau (`GET /health`, `GET /health/ready`, `POST
/api/register`, `POST /api/login`, `GET`/`PUT /api/save`) - viet lai kien truc noi bo nay KHONG
can sua bat ky dong code Godot nao (`BackendClient.cs` van dung nguyen).

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

## San sang cho quy mo lon hon (rate limit, connection pool, health check)

Backend nay da co san mot so thanh phan CAN THIET de chay tot o quy mo VUA (hang nghin nguoi
choi/ngay) VA de sau nay mo rong tiep khi that su can:

- **Gioi han so ket noi Postgres moi instance** (`PG_POOL_MAX`, mac dinh 10) - tranh 1 instance
  chiem het gioi han ket noi cua database (Postgres mac dinh chi cho ~100 ket noi dong thoi).
- **Gioi han toc do request** (`express-rate-limit`) - 120 request/phut/IP cho API noi chung,
  rieng dang nhap/dang ky chi 20 lan/15 phut/IP (chong brute-force mat khau).
- **2 health endpoint rieng biet** cho orchestrator/load balancer:
  - `GET /health` (liveness) - process con song khong, luon nhanh, khong cham DB.
  - `GET /health/ready` (readiness) - instance nay co ket noi duoc Postgres khong, dung de load
    balancer QUYET DINH co nen gui request toi instance nay hay khong.
- **Tat mem (graceful shutdown)** - khi nhan SIGTERM (Kubernetes/ECS... gui truoc khi giet
  container luc trien khai/scale), doi request dang xu ly xong roi moi dong, tranh mat du lieu
  luu giua chung.
- **helmet** (HTTP security header co ban) va **trust proxy** (de rate-limit/log dung IP nguoi
  choi that thay vi IP cua load balancer/proxy dung truoc).

### Gioi han THAT SU (khong the "sua bang code")

Backend hien tai van la **1 instance API + 1 Postgres duy nhat** (dung `docker compose`). Cac
thay doi tren giup no KHONG SAP khi tai tang dan va SAN SANG de mo rong, nhung **chua tu no dat
duoc quy mo hang trieu nguoi choi dong thoi** - dieu do can ha tang THAT SU (khong the tao bang
cach sua file trong repo, va toi khong the tu trien khai/kiem tai ha tang that duoc):

1. **Nhieu instance API sau load balancer** - vi backend da khong luu trang thai trong bo nho
   (JWT stateless, moi ket noi DB qua pool), co the chay THANG nhieu container `api` giong het
   nhau (Kubernetes Deployment + HorizontalPodAutoscaler, hoac AWS ECS/Fargate, hoac Render/
   Railway o che do "nhieu instance") ma khong can sua code them.
2. **PgBouncer** (connection pooler o giua API va Postgres) - khi so instance API tang len hang
   chuc/hang tram, tong so ket noi (`PG_POOL_MAX` x so instance) se vuot gioi han cua Postgres du
   moi instance da gioi han rieng. PgBouncer gop hang ngan ket noi "ao" tu cac instance API thanh
   1 so luong ket noi "that" nho hon toi Postgres.
3. **Nang cap Postgres / doc-replica / sharding** - 1 Postgres duy nhat (du manh) cuoi cung van
   se la nut that co (tat ca ghi/doc save deu qua 1 noi). O quy mo rat lon can: doc-replica (tach
   luong doc save ra khoi luong ghi), hoac chia (shard) du lieu nguoi choi theo playerId ra nhieu
   database, hoac chuyen sang dich vu quan ly san co kha nang tu mo rong (Amazon Aurora
   Serverless, CockroachDB, PlanetScale...).
4. **Cache (Redis)** - neu them cac API doc nhieu/ghi it (vd bang xep hang, thong tin cong khai),
   nen cache o Redis thay vi doc thang Postgres moi lan.
5. **CDN/DDoS protection o tang mang** (Cloudflare, AWS Shield...) - rate-limit trong code (da co
   o tren) chi la lop phong ve o TANG UNG DUNG, khong chan duoc tan cong quy mo lon o tang mang.
6. **Giam sat/canh bao** (Prometheus+Grafana, hoac dich vu APM nhu Datadog/New Relic) - bat buoc
   phai co de BIET khi nao he thong sap qua tai TRUOC KHI nguoi choi that gap loi, chu khong doi
   loi xay ra roi moi biet.

Day la khoi luong ha tang/chi phi van hanh o quy mo cua 1 doi ky thuat rieng, khong phai thu co
the hoan thanh chi bang cach sua code trong repo nay - neu ban thuc su can toi quy mo do, nen bat
dau tu buoc 1 (nhieu instance + load balancer tren 1 nen tang PaaS co san autoscaling nhu Render/
Railway/Fly.io) va do luc luong THAT SU truoc khi dau tu tiep vao cac buoc sau.
