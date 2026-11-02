# Zebra TLP 2844 USB kurulum kılavuzu

## Doğru modeli belirleyin

Cihaz etiketinde **TLP 2844** yazmalıdır. **TLP 2844-Z**, ZPL ailesindedir; bu
programın RAW EPL2 yolunda kullanılmaz. Kuyruk adını değiştirmek, yazıcının dilini
değiştirmez. Uygulama RAW gönderim için kurulu sürücü adında TLP 2844 modelini arar
ve 2844-Z/ZPL adlarını reddeder. Bu kontrol, cihazın gerçek modelinin fiziksel
olarak doğrulanmasının yerine geçmez.

TLP 2844 artık Zebra'nın desteklediği güncel bir ürün değildir. Bu nedenle Windows
11'de her sürücü/USB/firmware birleşimi için sorunsuz çalışma garantisi verilemez.
Uygulama Windows 11 x64 için derlenmiştir; sürücü ve gerçek baskı kabulü hedef
bilgisayarda ayrıca tamamlanmalıdır. Firmware güncellemesi bu kurulumun parçası
değildir; uygulama firmware yüklemez.

## Windows kuyruğunu hazırlayın

1. Yazıcı modelini, bağlantı kablosunu, etiket/ribon türünü kontrol edin.
2. Zebra'nın resmi kaynaklarından TLP 2844 EPL modelini içeren, bilgisayarınız için
   uygun imzalı sürücü paketini edinin. Bir üretici kurulum paketi gerekiyorsa
   bunu internet olan bir bilgisayarda indirip çevrimdışı bilgisayara taşıyabilirsiniz.
3. Zebra'nın USB yönergesine göre sürücüyü USB bağlantısından **önce** kurun.
4. USB kablosunu doğrudan bilgisayara bağlayın. Zebra'nın önerdiği kısa kabloyu
   kullanın; eski cihaz USB 1.1 iletişim kurar. İlk testte USB çoğaltıcı kullanmayın.
5. Windows **Ayarlar → Bluetooth ve cihazlar → Yazıcılar ve tarayıcılar** bölümünde
   kuyruğun oluştuğunu doğrulayın. Sürücü modeli TLP 2844 EPL olmalıdır. Bağlantı
   noktası çoğunlukla `USB001` benzeri görünür; doğru portu kendi bilgisayarınızdan
   doğrulayın. Uygulama sabit bir USB port numarası varsaymaz.
6. Kuyruk yoksa sürücü kurulumunu ve Aygıt Yöneticisi'ni kontrol edin. Programın
   yazıcı listesi kurulu Windows kuyruklarını gösterir; ham USB aygıtlarını taramaz.

## Medya ve ilk baskı

1. İlk test için **50 mm genişlik × 30 mm yükseklik, 3 mm boşluk** olan tek sütun
   etiketi takın. Yazıcı kapalıyken kullanıcı kılavuzundaki doğru yükleme yolunu
   izleyin. Kapağı kapatın. Termal transfer kullanıyorsanız uygun ribon takın.
2. Zebra TLP 2844 Label Designer'da **Yazıcı ayarları → Yazıcıları yenile** ile kuyruğu seçin.
3. Ribonsuz termal kağıtta **Direkt termal** seçili olsun; ribonlu termal transferde
   seçimi kaldırın. Yoğunluk 8, hız kodu 2 ile başlayın.
4. Aralıklı medya kullanırken **Etiket kalibrasyonu** ile `xa` gönderin. Bu işlem
   medya ilerletir ve boşluğu algılar. Sürekli medyada `xa` kullanmayın; tasarımda
   boşluğu 0 seçin. Siyah işaretli medya bu sürümün kapsamı dışındadır.
5. **50 × 30 mm EPL2 test baskısı** düğmesine basın ve belirtilen ölçüyü onaylayın.
   Bu düğme tasarım boyutlarından bağımsız olarak tek bir 50 × 30 test etiketi basar.
6. Üstte `ETIKET STUDIO`, altında Code 128 ve `123456789012` görülmelidir. Okuyucuyla
   tarayın. Çizgiler kopuk veya birleşikse kafa temizliği, etiket/ribon, yoğunluk
   ve hızı kontrol edin; ilk düzeltme olarak düşük hızda deneyin.
7. Ürün ve tasarım ekleyip Türkçe metin ve QR kod içeren bir etiketi önizleyin ve
   basın. `ç ğ ı İ ö ş ü` karakterlerini ve QR taramasını ayrı doğrulayın.

Yazıcının EPL2 `GW` komutu ikili görüntü taşır. Türkçe metin ve QR görüntü olarak
gönderildiği için yazıcının dahili Türkçe yazı tipine veya QR firmware komutuna
bağımlılık yoktur. Yerleşik test etiketi ayrıca yazıcının `A` ve `B` komutlarını
kullanır. Program ZPL komutu üretmez.

## Sürücü üzerinden baskı

**Windows sürücüsüyle yazdır** seçeneğini etkinleştirin. Windows yazdırma
tercihlerinde aynı özel etiket boyutunu tanımlayın. Hız, yoğunluk ve termal medya
türünü bu yolda sürücüden ayarlayın. Uygulama isteği sürücüyle doğrular; boyut veya
yazdırılabilir alan uymuyorsa ölçekleyerek baskı yapmaz. Varsayılan EPL2 yolu,
TLP 2844 için ilk denenecek yoldur.

## Sorun giderme

| Belirti | Kontrol |
| --- | --- |
| Yazıcı listede yok | USB, güç, Windows kuyruğu ve sürücü; ardından listeyi yenileyin |
| Listede var ama etiket çıkmıyor | USB portu, Windows bekleyen işler, medya, kapak, ribon; kabul edilen iş fiziksel baskı demek değildir |
| EPL komutları düz metin basılıyor | Cihazın tanılama/dump durumunda olup olmadığını kontrol edin; kullanıcı kılavuzuna göre normal moda dönün |
| Her seferinde etiket atlıyor | Etiket yüzey yüksekliği ile boşluk ölçüsünü ayırın; aralıklı medyayı kalibre edin |
| Türkçe metin eksik | Önizlemeyi kontrol edin; bilgisayarda seçili yazı tipi bulunmalı ve metin alanına sığmalıdır |
| Barkod taranmıyor | Boyut, beyaz alan, fiziksel yoğunluk/hız ve çizgi bütünlüğünü kontrol edin |
| “Sonuç belirsiz” | Yeniden göndermeden önce fiziksel etiketleri ve Windows kuyruğunu kontrol edin |

Uygulama medyanın bittiğini yalnızca Windows bunu raporluyorsa görebilir; USB
üzerinden donanımsal iki yönlü doğrulama uygulanmamıştır. Kabul formunu doldurmadan
cihazı üretim baskısı için doğrulanmış saymayın.

## Resmi başvuru kaynakları

- [Zebra: 28xx/38xx USB iletişimi](https://support.zebra.com/article/000016564)
- [Zebra: 2844 yapılandırmasını görme](https://support.zebra.com/article/000015691)
- [Zebra EPL Programlama Kılavuzu](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/epl2-pm-en.pdf)
- [Microsoft: eski yazıcı sürücülerine ilişkin plan](https://learn.microsoft.com/windows-hardware/drivers/print/end-of-servicing-plan-for-third-party-printer-drivers-on-windows)

Kaynaklar 31 Ekim 2026 tarihinde incelenmiştir. Üretici sürücüsü ve kılavuzları bu
GPL paketi içinde yeniden dağıtılmamıştır.
