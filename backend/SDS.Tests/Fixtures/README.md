# Anonim fixture'lar (T0a-00)

Gerçek optik çıktının (`ornek_optik_cikti.txt`) ve OBİS sınıf listesinin (`Sinif_Listesi.xlsx`) yapısı korunmuş, ad ve numaraları değiştirilmiş kopyalarıdır. Aşağıdaki tüm değerler uydurmadır; gerçek veri yoktur. Satır sonları `.gitattributes` ile korunur (`-text`); Windows-1254 varyantı commit'lenmez, testte UTF-8 dosyadan üretilir.

- `optik-ornek-anonim.txt`: 17 satır (2 anahtar + 15 öğrenci), UTF-8 (BOM yok), CRLF, son satır dolgusuz ve satır sonsuz. Numara 0-8, tür 18, kitapçık 20, ad 21-40 (0 tabanlı).
- `sinif-listesi-ornek-anonim.xlsx`: sayfa `Sayfa1`, başlıklar `OGRENCINO`, `ADSOYAD`, `SINAVNOTU` (boş), 17 veri satırı, numara sayısal.

## Optik satırların beklenen eşleştirme sonucu

| Satır | Numara | Ad | Kitapçık | Sonuç |
|---|---|---|---|---|
| 1 | 000000000 | (boş) | A | cevap anahtarı |
| 2 | 000000000 | (boş) | B | cevap anahtarı |
| 3 | 866407542 | DECYUM OM | A | tam |
| 4 | 396457865 | BUZGAY TIJG | A | tam |
| 5 | 306407573 | ROLVOS KISA | A | tam |
| 6 | 386457851 | DECYUM AMİ HÖKOT | B | tam |
| 7 | 386407862 | EĞAN ZOC VURA | B | tam |
| 8 | 326457817 | TUTÜP AFAG | A | tam |
| 9 | 306457525 | IHI KOĞĞINOG | B | tam |
| 10 | 326407871 | NEBA YACSUT TURLU | B | tam |
| 11 | `3064578 9` | AKBOD DECYUM | B | öneri: iç boşluk, listede 306457819 (aynı ad) |
| 12 | 866407854 | AKBOD SEJEZ YOVE | A | tam |
| 13 | 326457892 | HIHDIK MUNILİ | B | tam |
| 14 | 326407866 | ÜHKOÜT ZOLZI | A | öneri: tek hane farkı + aynı ad, listede 396407866 |
| 15 | 306407864 | UMĞEYUG BÜGE | B | tam |
| 16 | 866457827 | AMİ NEM TEB | A | tam |
| 17 | 396407815 | KETÜD CEYZINİRCÖ | B | tam |

## Sınıf listesinin beklenen sonucu (listede olup optikte tam eşleşmeyen 4 satır)

| Liste numarası | Ad | Sonuç |
|---|---|---|
| 306457819 | AKBOD DECYUM | optik satır 11 için öneri (iç boşluk) |
| 396407866 | ÜHKOÜT ZOLZI | optik satır 14 için öneri (tek hane + aynı ad) |
| 326407875 | AMİ JAHÜ | sınava girmedi; 326407871 (NEBA YACSUT TURLU) ile tek hane farkı ama ad farklı: öneri yok (tuzak) |
| 386457817 | UCĞA JIBTA | sınava girmedi; 326457817 (TUTÜP AFAG) ile tek hane farkı ama ad farklı: öneri yok (tuzak) |

Kalan 13 liste satırı optik satırlarla tam eşleşir (aynı numara, aynı ad).

## Beklenen özet

Liste 17 öğrenci. Optik 15 öğrenci + 2 anahtar satırı. 13 tam eşleşme, 2 öneri, 2 sınava girmedi.
