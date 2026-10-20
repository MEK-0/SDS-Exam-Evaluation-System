# Mimari yaklaşım

SDS, frontend’de özellik bazlı modüler yapı ve backend’de Clean Architecture katmanları kullanır.

## Backend katmanları

- `SDS.Domain`: Framework bağımsız domain çekirdeği.
- `SDS.Application`: Use case, sözleşme ve uygulama servisleri.
- `SDS.Infrastructure`: PostgreSQL ve EF Core gibi teknik uygulamalar.
- `SDS.Api`: HTTP endpoint’leri, middleware ve dependency injection composition root.
- `SDS.Tests`: Katman davranışlarını doğrulayan xUnit testleri.

## Bağımlılık yönü

`Api → Application → Domain` ve `Api → Infrastructure → Application/Domain` yönü korunur. Controller yalnızca uygulama servisini çağırır; iş kuralı taşımaz.

## Frontend

`app` rota ve uygulama girişini, `components` tekrar kullanılabilir arayüzü, `features` iş alanlarını, `types` ortak modelleri ve `styles` görsel temeli taşır. Phase 1 mock verileri feature içinde tek dosyada tutulur.
