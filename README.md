# SDS — Sınav Değerlendirme Sistemi

SDS, Selçuk Üniversitesi bağlamında öğretim üyelerinin sınav değerlendirme süreçlerini yönetmesini kolaylaştırmak amacıyla geliştirilen web tabanlı bir yazılımdır.

## Temel özellikler

- Kullanıcı kimlik doğrulama
- Rol tabanlı yetkilendirme
- Akademik yıl ve ders yönetimi
- Sınav taslağı oluşturma
- Optik TXT dosyası içe aktarma
- XLSX sınıf listesi okuma
- Cevap anahtarı yönetimi
- Çoklu kitapçık desteği
- Kitapçıklar arasında soru eşleştirme

Aşağıdaki özellikler henüz tamamlanmamıştır:

- Öğrenci ve optik kayıt eşleştirme algoritması
- Otomatik puanlama motoru
- Öğrenme çıktısı analizi
- Raporlama ve Excel dışa aktarma
- Tam kapsamlı yönetim paneli

## Teknoloji yığını

### Backend

- ASP.NET Core 8 Web API
- Entity Framework Core
- PostgreSQL
- JWT Authentication
- xUnit

### Frontend

- React
- TypeScript
- Vite
- Tailwind CSS
- React Router
- TanStack Query
- Axios
- Lucide React
- Recharts

### Geliştirme araçları

- Docker Compose
- Git
- .NET CLI
- npm

## Gereksinimler

- Node.js 20 veya üzeri
- .NET 8 SDK
- Docker Desktop

## Kurulum

Yerel ortam dosyasını oluşturun:

```bash
cp .env.example .env
```

`.env` dosyasındaki veritabanı, JWT ve development seed değerlerini yalnızca yerel ortam için güvenli değerlerle doldurun. `.env` Git takibine alınmaz.

PostgreSQL'i başlatın:

```bash
docker compose up -d
```

Backend'i Development ortamında çalıştırın. `backend/SDS.Api/Properties/launchSettings.json` dosyası bu ortamı otomatik seçer. JWT anahtarı, veritabanı bağlantısı ve seed parolaları Git'e alınmayan `.env` dosyasından yüklenir:

```bash
set -a
source .env
set +a
cd backend
dotnet restore
dotnet run --project SDS.Api
```

İsterseniz aynı hassas ayarları `.env` yerine .NET User Secrets ile tanımlayabilirsiniz. Değerleri yerel ve güvenli değerlerle doldurun; gerçek parolaları terminal çıktısına veya kaynak koda eklemeyin:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:SdsDatabase" "<yerel-postgresql-bağlantı-cümlesi>" --project SDS.Api
dotnet user-secrets set "Jwt:Key" "<en-az-32-karakterlik-yerel-anahtar>" --project SDS.Api
dotnet user-secrets set "Jwt:Issuer" "SDS.Api" --project SDS.Api
dotnet user-secrets set "Jwt:Audience" "SDS.Frontend" --project SDS.Api
dotnet user-secrets set "Seed:AdminPassword" "<yerel-admin-parolası>" --project SDS.Api
dotnet user-secrets set "Seed:AdminInstructorPassword" "<yerel-admin-instructor-parolası>" --project SDS.Api
dotnet user-secrets set "Seed:InstructorPassword" "<yerel-instructor-parolası>" --project SDS.Api
dotnet run --project SDS.Api
```

Frontend'i ayrı bir terminalde çalıştırın:

```bash
cd frontend
npm install
npm run dev
```

API sağlık kontrolü için `http://localhost:5000/api/health` adresini kullanabilirsiniz.

## Migration ve development seed

Development ortamında API başlarken mevcut EF Core migration'ları uygulanır. Development seed yalnızca development ortamında ve veritabanında kullanıcı bulunmuyorsa çalışır.

Yeni migration üretmek için:

```bash
cd backend
dotnet tool install --global dotnet-ef --version 8.0.11
dotnet ef migrations add MigrationName --project SDS.Infrastructure --startup-project SDS.Api --output-dir Persistence/Migrations
dotnet ef database update --project SDS.Infrastructure --startup-project SDS.Api
```

Seed kullanıcılarının kullanıcı adları ve parolaları yalnızca yerel `.env` yapılandırmasından okunur. Parolalar kaynak kodda veya README içinde tutulmaz.

## Proje mimarisi

Backend Clean Architecture yaklaşımını izler:

- `SDS.Domain`: Framework bağımsız domain varlıkları ve kurallar
- `SDS.Application`: Kullanım senaryoları, DTO'lar ve uygulama sözleşmeleri
- `SDS.Infrastructure`: EF Core, PostgreSQL, parser ve teknik servisler
- `SDS.Api`: HTTP endpoint'leri, authentication ve dependency injection composition
- `SDS.Tests`: Uygulama ve altyapı davranış testleri

Frontend özellik bazlı yapılandırılmıştır. Ortak layout ve UI bileşenleri `components`, özellik akışları `features`, API çağrıları `services` ve sorgu yönetimi `hooks` altında bulunur.

Ayrıntılı mimari açıklama için [`docs/architecture/ARCHITECTURE.md`](docs/architecture/ARCHITECTURE.md) dosyasına bakabilirsiniz.

## Test ve doğrulama

Backend:

```bash
cd backend
dotnet restore
dotnet build
dotnet test
```

Frontend:

```bash
cd frontend
npm run typecheck
npm run build
```

## Geliştirme kapsamı

Kodlama standartları ve yerel çalışma talimatları public repository dışında tutulur. Public repository yalnızca uygulamanın çalışması için gerekli kaynak kodu ve kullanıcı dokümantasyonunu içerir.

## Lisans

Bu repository için henüz lisans dosyası tanımlanmamıştır.
