# TBRK Label Studio kullanım kılavuzu

Sürüm 0.1.3 · Windows 11 x64 · Türkçe kılavuz

## Başlangıç ve ilk etiket

Uygulama ZIP dosyasını tümüyle bir klasöre çıkarın ve `TBRKLabelStudio.exe` dosyasını
çalıştırın. .NET çalışma zamanı uygulamaya dahildir; ayrıca kurmanız gerekmez.
`docs` ve `examples` klasörlerini EXE yanında tutun. Normal kullanım çevrimdışıdır.

Önce yazıcı kurulum kılavuzunu uygulayın. **Yazıcı ayarları** ekranında TLP 2844 EPL
kuyruğunu seçin. Ribonsuz, direkt termal etiket için **Direkt termal (ribonsuz)**
seçili olsun. Termal transfer etiket ve ribon kullanıyorsanız seçimi kaldırın.
Başlangıçta yoğunluk 8 ve hız kodu 2 kullanın. 50 × 30 mm etiket ve 3 mm boşluk
olan ruloyu taktıktan sonra **50 × 30 mm EPL2 test baskısı** düğmesine basın.
Onayladığınızda gerçek bir Code 128 etiketi gönderilir. Basılan barkodun tarama
sonucu `123456789012` olmalıdır. Kuyruğa gönderilmesi fiziksel baskı kanıtı değildir.

## Ürün yönetimi

**Ürünler → Yeni** ile ürün adı ve stok kodu girin. Barkod, fiyat, kategori,
açıklama ve görsel ekleyebilirsiniz. **Kaydet** düğmesine basın. Stok kodları
benzersiz olmalıdır. Fiyat negatif olamaz; en fazla iki ondalık basamak kullanın.
Mevcut ürünü değiştirmek için satırını seçin, alanları düzenleyin ve başka satıra
geçmeden **Kaydet** düğmesine basın. **Sil**, onayınızla ürünü kaldırır. Şablonlar
silinmez. Arama kutusu ürün adı, stok kodu, barkod ve kategoriyi tarar.

PNG, JPEG ve BMP görselleri kullanılabilir. Görsel başına en fazla 5 MB ve
4096 × 4096 piksel desteklenir. Ürün görseli veritabanında saklanır. Tasarımdaki
resim nesnesi önce kendisine eklenmiş görseli, yoksa seçili ürünün görselini
kullanır. Termal baskı siyah beyazdır.

## Excel ve CSV işlemleri

**İçe aktar** ve **Dışa aktar**, `.xlsx` ve `.csv` dosyalarını destekler. `.xls`
dosyalarını önce Excel ile `.xlsx` biçimine dönüştürün. Excel dosyasının ilk
çalışma sayfası okunur. Hazır örnekler `examples/products.xlsx` ve
`examples/products.csv` içindedir.

| Başlık | İçerik | Zorunlu |
| --- | --- | --- |
| Sku / Stok kodu | Benzersiz ürün kodu | Evet |
| Name / Ürün adı | Ürün adı | Evet |
| Barcode / Barkod | Barkod verisi; baştaki sıfırları korumak için metin hücresi kullanın | Sütun zorunlu; barkod basılana kadar boş olabilir |
| Price / Fiyat | `12,50` veya `12.50`; binlik ayırıcı kullanmayın | Evet |
| Description / Açıklama | Açıklama | Hayır |
| Category / Kategori | Kategori | Hayır |
| ImageBase64 | Base64 biçiminde gömülü görsel | Hayır |

CSV için UTF-8, isteğe bağlı BOM, virgül/noktalı virgül ayırıcıları, tırnaklı
alanlar ve çok satırlı açıklamalar desteklenir. Excel barkod ve stok kodlarını
metin olarak dışa aktarır. Excel'in daha önce sildiği baştaki sıfırlar geri
getirilemez; girişten önce hücre biçimini **Metin** yapın. Formülleri içe aktarmadan
önce değerlere dönüştürün. Güvenmediğiniz formül benzeri metinleri paylaşırken
XLSX dışa aktarmayı tercih edin; bazı tablo programları CSV alanlarını formül
olarak yorumlayabilir.

İçe aktarmada tüm satırlar kontrol edilir. Aynı stok kodu mevcutsa ürün güncellenir;
yeni kod eklenir. Dosya içinde aynı stok kodunun tekrarı reddedilir. Hata oluşursa
işlem kısmen kaydedilmez. En fazla 10.000 satır işlenir. Büyük Base64 görselleri
Excel hücre sınırını aşabilir; bu durumda CSV kullanın veya görseli küçültün.

## Etiket tasarlama

**Etiket tasarımı** ekranında şablon adı, genişlik, yükseklik ve etiket aralığını
belirleyin. Genişlik ve yükseklik etiketin yüzey ölçüsüdür; boşluk ayrıca yazılır.
`examples/templates` klasöründe üç örnek vardır. **Aç** ile yükleyebilir, **Kaydet**
ile `.etiket.json` dosyası oluşturabilirsiniz. Şablona eklenen görseller JSON
içinde saklandığı için ayrı resim dosyasına ihtiyaç duyulmaz.

Soldaki aracı etikete sürükleyin veya çift tıklayın. Nesneyi seçip sürükleyerek
taşıyın. Sağ panelde X/Y konumu, genişlik/yükseklik, yazı tipi, punto, kalınlık,
çizgi kalınlığı, barkod türü ve barkod alt yazısı düzenlenir. Tüm konumlar mm'dir.
**Çoğalt**, **Sil**, **Öne getir**, **Arkaya gönder** nesneleri yönetir. Tuvalde
seçili nesneyi ok tuşlarıyla 0,5 mm taşıyabilir, Delete ile silebilirsiniz.
Ctrl+Z / Ctrl+Y tasarım değişikliklerini geri alır/yineler. Izgaraya hizalama
0,5 mm adım kullanır. Nesne boyutunu sağ panelden değiştirin.

Metin, barkod, QR kod, çizgi, dikdörtgen, resim/logo, fiyat, ürün adı, ürün kodu,
tarih ve saat nesneleri vardır. Metin ve barkod içeriğinde şu alanlar kullanılabilir:
`{Name}`, `{Barcode}`, `{Sku}`, `{Price}`, `{Category}`, `{Description}`, `{Date}`,
`{Time}`. Fiyat Türk lirası biçimindedir. Tarih/saat bilgisayar saatinden gelir;
bir baskı grubundaki bütün etiketler önizleme öncesinde alınan aynı zamanı kullanır.

Code 128 yazdırılabilir ASCII karakterlerini, Code 39 büyük Latin harflerini,
rakamları ve desteklenen işaretlerini kabul eder. Türkçe barkod içeriği için QR
kullanın. EAN-13, EAN-8 ve UPC-A numarası tam ya da yalnızca son kontrol hanesi
eksik olarak girilebilir. Eksik kontrol hanesi oluşturulan barkoda eklenir;
yanlış tam numara sessizce değiştirilmez, baskı engellenir. Ürün kaydı otomatik
değişmez. **Barkod araçları** ile numarayı doğrulayabilir ve tasarımda seçili
barkod nesnesine uygulayabilirsiniz.

Kırmızı görünen nesneleri baskıdan önce düzeltin. Etiket dışına çıkan nesne,
alana sığmayan metin veya çok küçük barkod baskıyı durdurur. Barkod çizgileri tam
yazıcı noktalarına hizalanır; sığdırmak için ezilmez. Barkodun beyaz boşluklarını
başka nesnelerle kapatmayın.

## Tekli ve toplu baskı

Adedi belirleyip **Baskı önizleme** düğmesine basın. Sabit metin, doğrudan girilmiş
barkod, eklenmiş logo ve tarih içeren etiketler için ürün kaydı zorunlu değildir.
Şablonda ürün adı, fiyat, stok kodu, ürün görseli veya `{Barcode}` gibi alanlar varsa
önizleme penceresinin üstünden gerçek bir ürün seçin. Seçilmezse açıklama gösterilir;
örnek ürün bilgileri baskıya veya EPL2 dosyasına gönderilmez.

Önizleme penceresinde **Yazıcı** listesinden yazıcıyı seçin. Yeni bağladıysanız
**Yazıcıları yenile** düğmesine basın. Yazıcı olmadan geçerli etiketin önizlemesi ve
PNG/EPL2 dışa aktarımı çalışır. Yazdırırken hata olursa açıklama aynı pencerede kalır.
**Yazdır**, tüm görüntüler hazır ve yazıcı seçili olduğunda etkinleşir. **Toplu baskı** ekranında ürünleri işaretleyip her satır için
adet yazabilirsiniz. **Tümünü seç** mevcut ürün kataloğunun tamamını seçer; bu
seçime daha önce kayıtlı ürünler de dahildir. Sadece son içe aktardığınız ürünler
basılacaksa ilgili satırları ayrıca seçin.

Tüm ürünler baskı öncesinde doğrulanır. **Önceki / Sonraki** ile önizlemeleri
inceleyin. **Yazdır** gerçek Windows kuyruğuna gönderir; **İptal** göndermez.
Önizlemedeki görüntüyü PNG, grubun tamamını ikili EPL2 dosyası olarak kaydedebilirsiniz.
Önizleme ve yazdırma aynı siyah beyaz görüntüyü kullanır; 180° yön değişikliği de
önizlemeye yansır.

**Gerçek boyut** görünümü Windows ekran ölçeklemesini esas alır. Önizlemedeki
50 mm cetveli fiziksel cetvelle ölçün. **Program ayarları → Ekran boyut düzeltmesi**
alanına `50 / ölçülen mm` yazın. Bu yalnızca ekrandaki boyutu düzeltir. Baskı her
zaman yazıcının 8 nokta/mm çözünürlüğünü kullanır.

Varsayılan yol **EPL2 / RAW**'dır. **Windows sürücüsüyle yazdır** seçilirse XPS
yoluyla Windows sürücüsü kullanılır. Bu yolda hız, yoğunluk ve medya türünü
Windows yazdırma tercihlerinden ayarlayın. Sürücü etiket boyutunu ve baskı alanını
kabul etmezse etiket ölçeklenmez, işlem durdurulur.

Tek işlemde en fazla 1000 etiket gönderilir. Büyük etiketlerde önizleme belleği
128 MB sınırını aşarsa ürünleri daha küçük gruplara ayırın.

## Kuyruk ve hatalar

Kuyruğa kabul edilen işe Windows numarası verilir. İşin kuyruktan kaybolması,
fiziksel etiketin çıktığını kanıtlamaz. Etiketi gözle ve barkod okuyucuyla kontrol
edin. Eski USB sürücüleri bağlantı kesilmesini veya medya bitmesini hemen
bildirmeyebilir. Program Windows'un sağladığı durumu gösterir.

**Kuyruğu yenile** seçili yazıcının işlerini getirir. **Seçili işi iptal et**,
bu uygulamanın gönderdiği işi iptal etmeyi ister; yazıcıya ulaşmış etiketler geri
alınamaz. Alt çubuktaki **Gönderimi durdur**, RAW toplu gönderimde sonraki işlemleri durdurur;
devam eden eşzamanlı Windows çağrısının dönmesi beklenebilir. Sürücü işini Windows
kuyruğundan iptal edin. Sürücü modunda uygulama içi gönderim iptali pasiftir;
**Windows kuyruğunu aç** işlemi gönderim sırasında da kullanılabilir. Bir hata sonrasında otomatik tekrar yapılmaz. Yeniden
denemeden önce basılmış etiketleri ve bekleyen işleri kontrol edin.

**Gönderim geçmişi**, gönderilen ve başarısız/sonucu belirsiz işleri kaydeder.
Çökme sonrası kalan “Gönderiliyor” kaydı yeniden gönderilmez.

## Ayarlar ve yedekleme

Koyu tema anında uygulanır. İngilizce arayüz için **Program ayarları → English**
seçin; değişiklik hemen uygulanır ve otomatik kaydedilir. Yeniden başlatma gerekmez.
Türkçeye dönmek için **Türkçe** seçin. Ayrıca paketteki **English.cmd** dosyası
uygulamayı doğrudan İngilizce, **Turkce.cmd** Türkçe açar.

Veriler `%LOCALAPPDATA%\EtiketStudio\etiketstudio.db` dosyasındadır. **Veritabanını
yedekle**, WAL içindeki kaydedilmiş işlemleri de içeren tutarlı bir yedek üretir.
Günün ilk açılışında `Backups` klasörüne otomatik yedek alınır; son yedi günlük
başlangıç yedeği saklanır. Bu yedek gün içindeki son değişiklikleri içermez;
önemli değişikliklerden sonra ayrıca elle yedek alın.

**Program ayarları → Yedekten geri yükle** ile `.db` yedeğini seçin. Onaydan önce
kaydedilmemiş ürün ve tasarım değişikliklerinizi kaydedin. Seçilen yedek doğrulanır,
mevcut veriler `Backups/before-restore-...db` dosyasına yedeklenir ve geri yükleme
tek işlemde uygulanır. Hatalı dosya veya işlem hatası mevcut verileri kısmen değiştirmez.
Başarılı geri yüklemeden sonra program kapanır; uygulamayı yeniden açın.
Elle alınmış ve geri yükleme öncesi yedekler otomatik silinmez.

Kapanırken hatalı ayar varsa açıklama gösterilir: **Hayır** ile düzeltmeye dönün,
**Evet** ile kaydedilmemiş ayar ve tasarım değişikliklerinden vazgeçerek çıkın.
Kaydedilmemiş ürün formu için ayrıca onay sorulur. Daha önce kaydedilmiş ürünler silinmez.
Küçük ekranda veya yüksek Windows ölçeğinde çalışma alanındaki kaydırma çubuklarını kullanın.
`application.log` teknik hata ayrıntılarını içerir; paylaşmadan önce ürün bilgisi
veya yerel dosya yolu olup olmadığını kontrol edin.

Bu sürümün doğrulama durumu `TEST_REPORT.md`, gerçek yazıcı kabul adımları
`HARDWARE_ACCEPTANCE.md` içindedir. Fiziksel yazıcı testi bu teslimatta yapılmamıştır.

ZIP dosyasını açın ve TBRKLabelStudio-0.1.3 klasöründeki TBRKLabelStudio.exe dosyasını çalıştırın. İlk açılış İngilizcedir. Türkçe için App settings ekranında Türkçe seçin.
