# Futbol Simülasyonu — Mimari ve Yol Haritası

Bu belge projenin teknik yönünü tanımlar ve her aşamadan sonra güncellenir.
Bir özellik burada "tamamlandı" olarak yazıyorsa, ilgili testlerin geçtiği doğrulanmıştır.

## 0. Başlangıç durumu (2026-10-14)

| Konu | Durum |
|---|---|
| Unity | 6000.6.5f1 |
| Render pipeline | URP 17.6 (PC ve Mobile asset'leri hazır) |
| Girdi | Yalnızca yeni Input System (1.20) |
| Kurulu paketler | AI Navigation, Test Framework, Timeline, uGUI, Visual Scripting |
| Mevcut kod | Yok (yalnızca şablonun `TutorialInfo` klasörü) |
| Animasyon / model | Yok |
| Fizik | Varsayılan PhysX, 50 Hz sabit adım |

Korunması gereken bir oyun kodu olmadığından modüler mimari sıfırdan kuruluyor.
Şablonun `TutorialInfo` ve `Readme` dosyalarına dokunulmadı.

**URP mi HDRP mi?** URP. Gerekçe: 22 karakter, stadyum ve seyirci aynı anda çizilecek; hedef
orta segment PC ve ileride konsol/mobil olabilir. URP'nin Forward+ ve GPU Resident Drawer'ı bu
yükü kaldırır; HDRP'nin getirdiği fiziksel ışık birimleri ve volumetrikler oynanışa katkı vermez
ama kare bütçesini ciddi tüketir. Proje zaten URP ile açıldı, geçiş maliyeti yok.

## 1. Temel mimari kararları

### 1.1 Simülasyon ile görünümü ayırmak
Oyunun "beyni" Unity'ye bağımlı olmayan saf C# sınıflarında çalışır (`BallSimulator`,
`PlayerMotor`, ileride `ActionScorer`, `TeamShape`). MonoBehaviour'lar yalnızca bu sınıfları
sahneye bağlar. Bunun üç sebebi var:

1. **Test edilebilirlik.** Fizik ve AI kuralları Play mode'a girmeden EditMode testleriyle,
   milisaniyeler içinde ve deterministik olarak doğrulanır.
2. **Tahmin.** AI'ın "topa kim önce ulaşır" sorusunu cevaplaması için topun geleceğini
   simüle etmesi gerekir. Oyunda kullanılan fizik ile tahminde kullanılan fizik **aynı kod**
   olduğunda tahmin ile gerçek ayrışmaz. PhysX Rigidbody ile bu mümkün değildir.
3. **Toplu simülasyon.** Aşama 5'teki otomatik maç testleri görüntü olmadan, hızlandırılmış
   zamanla çalıştırılabilir.

### 1.2 Top fiziği: özel integratör, PhysX değil
Top; yerçekimi, karesel hava direnci, Magnus kuvveti (falso), dönüş sönümlenmesi, zemine
çarpma (geri sekme + sürtünme-dönüş etkileşimi), yuvarlanma direnci ve direk/üst direk
çarpışmalarıyla `BallSimulator` içinde 200 Hz alt adımlarla (50 Hz fizik karesi başına 4 adım) hesaplanır. Oyuncu gövdeleri
topa kapsül engel olarak kaydolur. Top hiçbir zaman oyuncuya bağlanmaz veya teleport edilmez;
oyuncunun topla her etkileşimi bir **vuruş/dokunuş darbesidir**.

### 1.3 Oyuncu hareketi: kodla kontrol, root motion yok
Oyuncu konumu tamamen `PlayerMotor` tarafından belirlenir (kinematik). Animasyonlar
(Aşama 2 ve 5) **yerinde** oynatılır ve motorun ürettiği hız/dönüş değerleriyle karıştırılır;
ayak kaymasını adım fazı eşleştirme ve ayak IK'sı azaltır. Root motion kullanılmaz çünkü:
AI'ın bir oyuncunun 1,2 saniye sonra nerede olacağını hesaplayabilmesi gerekir ve bu ancak
hareket modeli analitik olduğunda güvenilirdir. Root motion ile kod hareketi aynı anda
**kullanılmayacak**.

### 1.4 Karar mimarisi: üç katman
```
TeamTacticalAI  (2–4 Hz)   Faz, diziliş şekli, kim baskıya çıkar, rol atamaları
      │ rol + bölge + görev
      ▼
PlayerDecisionSystem (5–10 Hz, oyuncu başına kaydırılmış)
      Utility AI: eylem adaylarını üret → puanla → histerezis → seç
      │ eylem (hedef nokta, bakış yönü, vuruş isteği)
      ▼
PlayerMotor + BallInteraction (50–100 Hz)   Hareket, dokunuş, vuruşun fiziksel uygulanması
```
- **FSM** yalnızca kaba oyuncu durumu için (Normal, Duran Top, Yerde, Kutlama).
- **Utility AI** her eylem için çok faktörlü puan verir: başarı olasılığı × değer − risk,
  rol ve taktik ağırlıkları, kişisel eğilim. Ağırlıklar ScriptableObject'te durur ve
  testlerle ölçülür.
- **Histerezis:** seçili eylem bir sabit bonus alır, eylem değiştirme maliyeti vardır, en az
  süre dolmadan değiştirilmez. **Kesmeler** (top kaybı, rakip müdahalesi, açık şut fırsatı)
  bu kuralları atlar.

### 1.5 Ortak oyun durumu (algılama)
`MatchContext` her fizik adımında bir kez günceller: top durumu, 22 oyuncunun konum/hızı,
sahiplik. Pahalı hesaplar önbelleğe alınır ve sınırlı sıklıkla yenilenir:
- **Topa ulaşma süreleri** (her oyuncu, 10 Hz): top tahmini × motor hareket modeli.
- **Pas yolu tabloları** (topa sahip oyuncu için, 10 Hz): her alıcıya pas süresi ve rakiplerin
  kesme süresi.
- **Alan kontrol haritası** (takım başına, 4 Hz): sahayı ızgaraya böler, her hücreye hangi
  takımın önce ulaşacağını yazar. Boş alan ve koşu yolu kararları bunu okur.

Oyuncular dünyayı doğrudan sorgulamaz; bu tablolardan ve görüş açısı filtresinden okur.

## 2. Modüller ve klasörler

```
Assets/Football/
  Scripts/Runtime/      (Football.Runtime.asmdef)
    Core/        PitchDimensions, PitchRules, ExecutionOrder
    Ball/        BallSimulator, BallState, BallPhysicsParams, BallPhysicsSettings,
                 BallController, BallEventsMonitor
    Players/     PlayerMotor, MovementParams, MovementProfile, PlayerBody,
                 DribbleTouchModel, BallTouchController, IMotorCommandSource,
                 IMoveAdjuster, PlayerMovementController, PlayerBallInteraction
    Control/     FootballInput (Input System, kod içinde yapılandırılır)
    CameraRig/   BroadcastCamera
    Pitch/       PitchLineMesh
    Debugging/   BallTestKicker, FootballDebugHud, ScriptedCommandSource
  Scripts/Editor/       (Football.Editor.asmdef) PitchBuilder, Phase1SceneBuilder,
                        FootballAssetPaths, FootballTestRunner
  Tests/EditMode/       (Football.Tests.EditMode.asmdef)
  Data/          BallPhysicsSettings.asset, MovementProfile_Default.asset
  Generated/     Prosedürel saha meshleri (builder üretir, elle düzenlenmez)
  Materials/  Scenes/   Phase1_Pitch.unity (builder üretir)
```

Saf C# çekirdek (Unity sahnesine bağlı değil, testlerle doğrudan çalışır):
`BallSimulator`, `PlayerMotor`, `PlayerBody`, `DribbleTouchModel`, `BallTouchController`, `PitchRules`.
MonoBehaviour'lar bunları yalnızca sahneye bağlar.

İleride eklenecek modüller ve hangi aşamada geldikleri:

| Modül | Sorumluluk | Aşama |
|---|---|---|
| `MatchContext` | Ortak durum + önbellekli algılama tabloları | 2 |
| `PossessionManager` | Kim kontrol ediyor, son dokunan, kontrol kalitesi | 2 |
| `KickSolver` | İstenen hedef/hıza ulaşmak için vuruş vektörü + özellik bazlı hata | 2 |
| `PlayerDecisionSystem` | Utility AI + histerezis | 2–3 |
| `PlayerAnimationController` | Hız/yön/vuruş zamanlamasıyla animasyon, ayak IK | 2 |
| `TeamTacticalAI` | Faz, rol atama, baskı/kademe koordinasyonu | 3 |
| `FormationManager` | Dizilişlerin hücum ve savunma şekilleri | 4 |
| `GoalkeeperAI` | Pozisyon, kurtarış, çıkış | 4 |
| `MatchStateManager` | Taç, korner, aut, gol, faul, devreler | 4 |
| `PlayerAttributes` | 1–100 özellikler, etki tablosu | 2 (temel), 5 (tam) |
| `MatchStatistics` + `SimulationRunner` | Metrikler, toplu maç testleri | 3–5 |

## 3. En kritik teknik riskler

1. **AI'ın topun peşinden koşan sürüye dönüşmesi.** En yaygın başarısızlık. Önlem: kimin topa
   gideceğine takım katmanı karar verir (topa ulaşma süresine göre en fazla 1–2 oyuncu),
   diğerleri rol bazlı şekil pozisyonlarını alır. Yığılma metriği her aşamada ölçülür.
2. **Kararsızlık/titreme.** Utility puanları birbirine yakın olduğunda her karede eylem
   değişir. Önlem: histerezis, minimum süre, ölçülebilir "eylem değişim sıklığı" metriği.
3. **Animasyon ile fizik temasının uyuşmaması.** Ayak topa değmeden top gidebilir. Önlem:
   vuruş isteği, ayağın topa temas karesinde tetiklenir; motor oyuncuyu temas noktasına
   adım uzunluğunu ayarlayarak getirir. Animasyon paketi seçilmeden çözülemez (bkz. 4).
4. **Tahmin ve gerçek fiziğin ayrışması.** Önlem: tek fizik kodu (1.2).
5. **Performans.** 22 oyuncu × pas yolu × alan haritası CPU'yu yer. Önlem: zamana yayılmış
   güncelleme, ızgara bazlı alan haritası, Profiler ile ölçüm, gerekirse Burst/Jobs.
6. **Ayarlanacak parametre sayısı.** Yüzlerce ağırlık elle ayarlanamaz. Önlem: tüm ağırlıklar
   veri dosyalarında, otomatik simülasyon ve metrik karşılaştırması (Aşama 3'ten itibaren).

## 4. Hazır asset mi, özel kod mu?

| Alan | Karar | Not |
|---|---|---|
| Top fiziği, hareket, AI, taktik, kurallar | **Özel kod** | Projenin farkı burada |
| Saha geometrisi ve çizgiler | **Özel kod** (prosedürel) | Ölçüler tek kaynaktan, kural kodu ile aynı |
| Kamera | **Özel kod** | Cinemachine sonra eklenebilir; şu an gerekmiyor |
| Karakter modeli + rig | **Hazır asset** (Humanoid) | Lisansı ticari kullanıma uygun olmalı |
| Futbol animasyonları | **Hazır asset** (mocap paketi) | Yerinde (in-place) varyantları olmalı; seçim Aşama 2 başında |
| Stadyum, seyirci | **Hazır asset + özel** | Aşama 5 |
| Ağ fiziği | Önce basit sönümleme bölgesi, sonra Cloth veya kemikli ağ | Aşama 5 |

Görsel üretim araçlarının (ör. Higgsfield) çıktıları yalnızca **referans/konsept** olarak
kullanılacak; oyunda çalışan model kabul edilmeden önce rig, UV, materyal ve Unity uyumluluğu
ayrıca doğrulanacak.

## 5. Yol haritası ve kabul kriterleri

### Aşama 1 — Teknik temel  ✅ tamamlandı (2026-10-14)
Kapsam: proje yapısı, gerçek ölçülü saha, yayın kamerası, bir oyuncu, top, hareket ve top
fiziği, top sürme dokunuşları, debug araçları.

Kabul kriterleri ve nasıl doğrulandığı (EditMode = otomatik test, Play = Editor'da çalıştırarak):
- [x] Derleme hatasız; 46/46 EditMode testi geçiyor (0,11 sn). — `Football > Run EditMode Tests`
- [x] Saha FIFA ölçülerinde; ölçüler tek kaynaktan (`PitchDimensions`). — görsel kontrol
- [x] Sprint hızının %90'ına ~2,0 sn'de ulaşılıyor (kriter 1,8–3,0). — EditMode
- [x] Tam sprintte 180° dönüşte oyuncu önce fren yapıyor, 0,4 sn'den uzun süre ileri kayıyor. — EditMode
- [x] 90° keskin dönüş sprintte 0,2 sn'de 45°'den az; jogda belirgin daha hızlı. — EditMode
- [x] Geri geri < yana < ileri koşu hızı; vücut hedefe dönük kalıyor. — EditMode
- [x] Sprint dayanıklılık tüketiyor, yorgunken sprint yok, dinlenince toparlanıyor. — EditMode + Play
- [x] 8 m/sn yerden pas 12–25 m, 15 m/sn pas 30–55 m yuvarlanıp duruyor. — EditMode
- [x] Kayan top üst dönüş kazanıp yuvarlanmaya geçiyor; yuvarlanan topta kayma yok. — EditMode
- [x] Bırakılan top her sekmede alçalıyor ve yerde duruyor. — EditMode
- [x] Ters dönüş (backspin) topu daha uzağa taşıyor; hava direnci menzili kısaltıyor. — EditMode
- [x] Sağ/sol falso topu doğru yöne 1–5 m kıvırıyor. — EditMode
- [x] Direğe çarpan top geri sekiyor, içinden geçmiyor. — EditMode + Play
- [x] Ağlar topu durduruyor; gol/taç/aut sınıflandırması Law 9'a uygun. — EditMode + Play (2 gol algılandı)
- [x] Top ayağa yapışmıyor; ayrı dokunuşlarla ilerliyor, sprintte dokunuşlar daha uzun. — EditMode + Play
- [x] 10 sn düz top sürme 15 senaryoda (3 hız/kontrol × 5 tohum) top kaybı olmadan. — EditMode
- [x] 45°, 90°, 135°, 180° yön değiştirmede top yeni yöne taşınıyor; 180°'de taban durdurma. — EditMode
- [x] Tahmin (`Predict`) ile adım adım simülasyon aynı sonucu veriyor (< 5 cm). — EditMode

Aşama 1 sırasında bulunan ve düzeltilen hatalar (testlerle sabitlendi):
- Dokunuş hatası topun toplam hızına uygulanıyordu → sprintte top yana kaçıyordu. Artık hata,
  ayağın oyuncunun koşusuna göre verdiği itmeye uygulanıyor.
- Yana düzeltme sabit kazançla salınıma giriyordu → bir sonraki dokunuşa kalan süreye göre ölçekleniyor.
- Dönüşte bacaklar topa gitmiyordu → `SteerMove`: istek topun yönünü, bacaklar dokunuş noktasını hedefler.
- Geri dönüşte gövde topu itiyordu → taban durdurma + kendi dokunuşundan sonra kısa gövde muafiyeti.

Bilinen eksikler (uygulanmış gibi gösterilmiyor):
- Pas, şut, orta ve kullanıcı vuruş tuşları yok (sayı tuşları yalnızca **debug test vuruşları**). → Aşama 2
- İlk dokunuş/top kontrolü basit ("cushion"); özellik ve baskı etkisi yok. → Aşama 2
- Oyuncu özellikleri yok; `ballControl` geçici bir alan. → Aşama 2
- Karakter ve hareket animasyonları eklendi (2026-10-14, Aşama 1 sonrası): Tripo ile üretilen model, Mixamo
  iskeleti ve 9 Mixamo animasyonu (bekleme, yürüme, jog, geri jog, sola/sağa yana adım, koşu, pas, penaltı vuruşu).
  Hareket animasyonları 2D blend tree ile vücuda göre hıza bağlı; klip pozisyonları kliplerden ölçülen doğal hızlar
  (`PlayerAnimatorBuilder`). Eksikler: en hızlı klip 4,96 m/sn olduğu için sprintte animasyon ~1,7 kat hızlı
  oynuyor (sprint klibi gerekiyor); ayak IK'sı yok; pas/şut animasyonları Animator'da var ama oyun mantığına ve
  topa temas zamanına bağlı değil. → Aşama 2
- Kafa vuruşu, zıplama, omuz omuza mücadele, oyuncu–oyuncu çarpışması yok. → Aşama 2–3
- Top saha dışına çıkınca yalnızca olay üretiliyor ve 2 sn sonra ortaya konuyor; taç/korner/aut
  yeniden başlatmaları yok. → Aşama 4
- Oyuncu saha sınırlarında durmuyor. → Aşama 4 (MatchStateManager)
- Görsel: uzak çizgiler tek piksel altında kalıp kesik görünüyor (doku/MSAA ile çözülecek);
  ufukta stadyum yok, gri boşluk görünüyor. → Aşama 5
- Ağ görsel olarak sabit; fizikte sönümleyici düzlemler. → Aşama 5

Nasıl çalıştırılır:
1. `Football > Build Phase 1 Scene` (sahneyi sıfırdan üretir; sahneyi elle düzenlemeyin).
2. `Assets/Football/Scenes/Phase1_Pitch.unity` açılır, Play'e basılır.
3. WASD hareket, Shift sprint, Q topa dön (geri/yana koşu). 1–5 debug vuruşları, 6 falso yönü,
   R top ayağa, G penaltı noktası, F1 HUD.
4. Testler: `Football > Run EditMode Tests` (Console'a özet yazar) veya Test Runner penceresi.

Ayarlanabilir değerler: `Assets/Football/Data/BallPhysicsSettings.asset` (top fiziği, Play'de canlı),
`MovementProfile_Default.asset` (hareket), oyuncu üzerindeki `PlayerBallInteraction` (top sürme).

### Aşama 2 — Bireysel futbol davranışları
Kapsam: `MatchContext`, `PossessionManager`, ilk dokunuş/kontrol (özellik ve baskıya bağlı
hata), `KickSolver` (yerden pas, ara pas, havadan pas, şut, falsolu şut, zayıf ayak),
oyuncu özellikleri (temel set), tek oyuncu Utility AI (pas/şut/sürme), animasyon paketi
entegrasyonu ve ayak IK, kullanıcı kontrolleri (pas, şut, ara pas, orta).

Kabul kriterleri:
- Pas isabeti uzaklık, baskı, vücut açısı, zayıf ayak ve Kısa Pas özelliğiyle ölçülebilir
  biçimde değişiyor (ör. 20 m pas, 90 vs 40 Kısa Pas için hedeften ortalama sapma farkı).
- Kötü açıdan şut puanı, iyi konumdaki takım arkadaşına pas puanının altında kalıyor
  (birim testle).
- Kontrol kalitesi topun gelen hızı ve oyuncunun Top Kontrolü özelliğiyle değişiyor.
- Vuruşta top, ayağın temas karesinde ayrılıyor (animasyon olay zamanı ile fark < 1 kare).

### Aşama 3 — Küçük ölçekli takım AI'ı (3'e 3, 5'e 5)
Kapsam: `TeamTacticalAI` ilk sürüm, alan kontrol haritası, pas üçgenleri/destek, baskı + kademe,
karar histerezisi, `MatchStatistics` + başsız `SimulationRunner`.

Kabul kriterleri (≥ 50 simülasyon ortalaması, baseline'a göre):
- Topun 5 m çevresinde aynı takımdan ortalama ≤ 2 oyuncu.
- Topa sahip oyuncunun en az 2 açık pas seçeneği olduğu süre oranı ≥ %70.
- Eylem değişim sıklığı oyuncu başına ≤ 1,5/sn; tehlike anında tepki süresi ≤ 0,3 sn.
- Savunmada baskıya çıkan oyuncunun arkasında kademe oyuncusu bulunma oranı ≥ %70.

### Aşama 4 — Tam saha 11'e 11
Kapsam: 5 diziliş (4-3-3, 4-2-3-1, 4-4-2, 3-5-2, 4-1-4-1) ayrı hücum/savunma şekilleriyle,
taktik ayarları, kaleci AI'ı, `MatchStateManager` (başlama, devre, gol, taç, korner, aut,
temel faul, serbest vuruş, penaltı; ofsayt aşamalı), duran toplarda yerleşim.

Kabul kriterleri:
- 90 dk simüle maçta gol, şut, pas başarısı ve top sahipliği gerçekçi aralıkta
  (ör. 1,5–3,5 gol/maç, %75–88 pas başarısı).
- Savunma hattı kopukluğu metriği eşik altında; diziliş değişikliği ortalama konum
  haritalarında ölçülebilir fark yaratıyor.
- Uygulanmamış kurallar arayüzde ve belgede açıkça listeleniyor.

### Aşama 5 — Derinlik ve kalite
Tüm oyuncu özellikleri ve etki tablosu, taktik varyasyonları, gelişmiş animasyonlar
(çalım, müdahale, kafa, kaleci), model/forma varyasyonları, stadyum, ağ fiziği, otomatik test
paketleri.

### Aşama 6 — Optimizasyon ve cilalama
Profiler ölçümleri ve bütçeler (hedef: 11'e 11 maçta orta PC'de 60 FPS, AI < 3 ms/kare),
kamera ve kontrol iyileştirmeleri, HUD, gamepad, son oynanış testleri.

## 6. Bilinmeyenler

- **Hedef donanım:** varsayılan olarak orta segment PC (Windows). Konsol/mobil hedef varsa
  bütçeler değişir.
- **Karakter ve animasyon asset'leri:** projede yok. Aşama 2'nin animasyon kısmı için bir
  Humanoid futbol animasyon paketi seçilmeli (ücretli olabilir).
- **Kullanıcı kontrolü:** varsayılan olarak bir takım kullanıcıda, rakip AI'da. Yerel çok
  oyunculu veya izleme modu (AI'a karşı AI) da gerekiyorsa mimari zaten destekliyor.
