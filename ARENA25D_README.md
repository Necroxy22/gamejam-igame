# Arena 2.5D Isometric — Curie's Crucible: Equilibrium

Sistem tower defense 2.5D lengkap (kamera orthographic, arena statis 16:9) untuk Unity 2022.3 LTS / Unity 6,
target build WebGL. Alur: **Marie Curie di balkon → racik ramuan → lempar ke jalan → reaksi kimiawi zombie →
menara bertahan sampai wave terakhir.**

## Urutan setup (WAJIB)

1. **Tools → Curie's Crucible → 1. Generate 2.5D Arena Hierarchy**
   Membuat `Arena_25D`, Environment_Layers, 2 jalur waypoint, collider jalan, reticle, kamera.
2. **Tools → Curie's Crucible → 2. Setup Marie Curie Player**
   Membuat `MarieCurie_Player` (child Environment_Layers, sortingOrder 450), sprite placeholder
   Victorian-steampunk, dan menghubungkan `MarieCurie_ThrowOrigin` ke `ArenaThrowTargeting`.
3. **Tools → Curie's Crucible → 3. Generate Recipes & Wave Prefabs**
   Membuat 6 `PotionRecipeSO` di `Assets/Data/Recipes/`, 5 prefab zombie + boss di
   `Assets/Prefabs/Arena25D/`, dan `Game_Manager` (Alchemy + Tower + Wave) yang sudah terhubung.
4. **Tools → Curie's Crucible → 4. Generate Compact Dota-Style UI**
   Membuat Canvas uGUI bertema Compact Dota-Style Victorian-Steampunk (1920x1080 Scale With Screen Size),
   EventSystem, HUD Atas (Menara HP & Wave/Timer), Pojok Kiri Bawah (Grid 2x2 Bahan Dasar),
   Tengah Bawah (Tombol Bulat CRAFTING + 6 Slot Hotbar Ramuan dengan highlight terpilih),
   Popup 2-Kolom (Panduan Resep & Kuali Alkimia/pH Lerp/BREW/CLEAR), Main Menu, Pause,
   Victory, Game Over, Settings & Credits, serta otomatis mematikan debug OnGUI.
5. Save scene (Ctrl+S), lalu tekan **Play**.

Menu tambahan: *Rebuild Road Polygon*, *Regenerate Placeholder Art*, *Create Test Zombie*,
*Auto Rebuild Road Polygon*.

## Struktur file

| File | Fungsi |
|---|---|
| `Arena25D/Arena25DSetup.cs` | Tool arena + Gizmos + sprite placeholder prosedural (termasuk Marie & zombie). |
| `Arena25D/PathFollower25D.cs` | Gerak zombie (Catmull-Rom), skala per Y, sorting, flipX, `PushBack()` untuk knockback. |
| `Arena25D/ArenaThrowTargeting.cs` | Raycast jalan, reticle elips, proyektil parabola, integrasi AlchemyManager + Marie. |
| `Arena25D/Arena25DMath.cs` | Utilitas perspektif/sorting/geometri jalan. |
| `Arena25D/Arena25DCameraRig.cs` | Framing kamera adaptif (FitWidth) untuk WebGL. |
| `Player/MarieCurieController.cs` | Gerak balkon, flipX ke kursor, breathing, animasi lempar. || `Alchemy/AlchemyTypes.cs` | `IngredientType`, `PotionType`, `MixedPotion`, `AlchemyDatabase`. |
| `Alchemy/PotionRecipeSO.cs` | ScriptableObject resep (2 bahan + rentang pH + efek). |
| `Alchemy/AlchemyManager.cs` | 2 slot kuali, meteran pH, stok bahan, tas ramuan 6 slot, overlay & buku resep. |
| `Gameplay/ZombieType.cs` | `ZombieType` + nama/warna/stat default. |
| `Gameplay/ZombieChemicalEntity.cs` | `IArenaPotionTarget`: damage per afinitas, root, slow, knockback, mutasi, boss 2 tahap, drop bahan. |
| `Gameplay/TowerHealthManager.cs` | HP menara 10, damage dari `PathFollower25D.OnReachedEnd`. |
| `Gameplay/WaveManager.cs` | 5 wave (Tutorial → Boss), spawn kiri/kanan, hadiah, drop, mutasi. |
| `UI/UITheme.cs` | Palet warna dan gaya estetika Victorian-Steampunk Brass & Wood. |
| `UI/GameplayHUD.cs` | Controller HUD (Health, Wave, pH Bar Lerp, Cauldron, Ingredients, Potions). |
| `UI/RecipeBookPopup.cs` | Popup buku resep & ensiklopedia sains kimia ("i"). |
| `UI/MainMenuOverlay.cs` | Overlay menu utama (Play, Tutorial, Pengaturan, Kredit). |
| `UI/PausePanel.cs` | Panel pause game (Resume, Settings, Restart). |
| `UI/GameEndPanel.cs` | Panel kemenangan (Victory) & kekalahan (Game Over). |
| `UI/SettingsPopup.cs` | Slider volume audio BGM & SFX. |
| `UI/CreditsPopup.cs` | Panel kredit dan dedikasi ilmiah Marie Curie. |
| `UI/UIManager.cs` | Koordinator master UI dan event handler. |
| `UI/UnityObjectUtil.cs` | Helper `Ensure()` untuk fallback referensi `UnityEngine.Object` yang aman terhadap objek yang sudah di-Destroy. |
| `UI/GameplayUIBridge.cs` | Jembatan HUD minimalis ↔ gameplay: hotbar 6 slot, panel stok bahan, `Craft_panel` + laci resep geser, drag & drop, pause/opsi, volume audio. |
| `UI/AlchemyCraftingPopup.cs` | Popup laboratorium 2-kolom (panduan resep + kuali 2 slot, meter pH lerp, BREW/CLEAR). |
| `UI/CraftDragAndDrop.cs` | `CraftIngredientDraggable` (drag kartu bahan/slot kuali, klik cepat, buang ke luar panel) dan `CauldronSlotDropZone` (drop & swap slot). |
| `Gameplay/TowerPipeHarvester.cs` | 4 gelembung panen pipa menara (Basa/Pendingin/Asam/Katalis) ala Clash of Clans, produksi tiap 7 detik. |
| `Gameplay/ZombieIntroSpotlight.cs` | Spotlight sinematik + kartu Quick Info saat tipe zombie pertama kali muncul (blokir input sampai LANJUT). |
| `Gameplay/ChemicalFeedbackManager.cs` | Teks melayang (floating text) umpan balik reaksi kimia di atas zombie. |
| `Editor/MarieCurieSetup.cs` | Menu 2. |
| `Editor/AlchemyContentSetup.cs` | Menu 3. |
| `Editor/UISpriteAssetGenerator.cs` | Generator sprite procedural Victorian-Steampunk 9-slice. |
| `Editor/UISetupGenerator.cs` | Menu 4 (Generate Full Victorian UI & Menus). |
| `Editor/FindTargets.cs` | Helper pencarian objek (kompatibel Unity 2022 & 6). |

## Kontrol

| Tombol | Aksi |
|---|---|
| `A` / `D` atau `←` / `→` | Geser Marie di balkon (-0.85 .. 0.85) |
| Mouse | Aim reticle (hanya di jalan) |
| Klik kiri | Lempar ramuan slot terpilih (stok berkurang; klik di luar panel crafting = buang bahan) |
| `1` – `6` | Pilih slot ramuan tetap di hotbar |
| `R` | Racik 1× resep slot terpilih (atau brew kuali saat `Craft_panel` terbuka) |
| `Q` / `E` | Pilih ramuan sebelumnya / berikutnya |
| `F` | Kosongkan kuali (bahan dikembalikan) |
| `C` | Buka / tutup panel crafting (HUD) |
| `F1` | Toggle overlay status alkimia |
| `F2` / `I` | Toggle buku resep |
| `Esc` / `P` | Pause (menutup popup yang terbuka lebih dulu) |
| `Space` | Mulai wave berikutnya lebih cepat (saat jeda) |
| `Space` (spotlight) | Lanjutkan setelah kartu Quick Info zombie baru |

**Catatan hotbar:** gameplay utama memakai 6 slot ramuan **tetap** — slot `1`–`6` selalu memetakan ke kombinasi yang sama
(lihat `AlchemyManager.FixedSlotPotions`), sehingga `R` meracik langsung tanpa perlu menyusun kuali.
Kuali 2-slot (`Craft_panel`) tetap tersedia untuk meracik manual dan bereksperimen dengan pH.

## Sistem Alkimia

- **pH** = 7.0 + modifier bahan. Asam **−4.5**, Basa **+4.5**, Pendingin & Katalis **0**.
  Hasilnya di-clamp 0–14.
- **6 resep** (urutan bahan bebas):

| Ramuan | Kombinasi | pH | Efek |
|---|---|---|---|
| Gel Sabun Beku | Basa + Pendingin | 11.5 | Root 3s + slow |
| Kabut Asam Dingin | Asam + Pendingin | 2.5 | AoE + slow, kuat vs Basa |
| Ledakan Es Instan | Pendingin + Katalis | 7.0 | Damage besar + root 2s |
| Asam Pelarut Karat | Asam + Katalis | 2.5 | 3× damage vs Zombie Besi |
| Bom Netralisasi | Asam + Basa | 7.0 | Damage besar, kunci Boss tahap 2 |
| Busa Sabun Mengembang | Basa + Katalis | 11.5 | Knockback + slow |

- **Stok bahan**: mulai 3 per jenis, bertambah dari drop zombie dan gelembung panen pipa menara.
  Batas `maxStockPerIngredient` (default 6) berlaku untuk drop, panen, dan refund kuali.
- **Tas ramuan**: hotbar 6 slot tetap; ramuan terpilih yang dilempar akan berkurang 1x dari slot-nya.

## Reaksi Zombie

| Zombie | Kelemahan | Catatan |
|---|---|---|
| Zombie Asam | Basa & Netralisasi | Tahan asam |
| Zombie Basa | Asam | Tahan basa |
| Zombie Api | Pendingin & Es | Bergerak cepat |
| Zombie Besi | Asam Pelarut Karat (3×) | Lambat, HP tinggi |
| Boss Unstable Amalgam | Dinginkan 3× → netralkan | Tahap 1 sangat tahan (12% damage) |

**Boss 2 tahap**: setiap ramuan pendingin menambah 1 Chill stack (maks 3). Saat 3/3, Boss menjadi
*Chilled* selama 8 detik (tint biru + label status). Selama Chilled, lempar **Bom Netralisasi** untuk
mengeksekusi Boss. Kalau 8 detik lewat, stack reset.

## Mutasi Zombie

Diberikan oleh `WaveManager` sesuai `mutationChance` wave:

- `Mutate_Enlarge()` — skala ×1.3 (lewat `PathFollower25D.baseScale`), HP +50%.
- `Mutate_SpeedUp()` — kecepatan +40%.
- `Mutate_Harden()` — damage masuk −40%.
- `Mutate_AoEBuff()` — aura radius 2.5 mempercepat zombie sekitar +25%.

## Wave

| Wave | Nama | Isi | Hadiah |
|---|---|---|---|
| 0 | Tutorial | 3 Zombie Basa | 2 ramuan |
| 1 | Easy | 5 Zombie Asam | 2 ramuan starter, drop 1× |
| 2 | Normal | 4 Asam + 3 Basa + 2 Api | drop 2×, mutasi 30% |
| 3 | Hard | 3 Api + 2 Besi + 3 Basa + 2 Asam | drop 3×, mutasi 50% |
| 4 | Boss | 3 Besi + Boss Unstable Amalgam | drop 5× |

Menara mulai dengan **10 HP**. Setiap zombie yang mencapai ujung path mengurangi HP (Boss −5).
Kelima wave (termasuk wave Boss) dijalankan berurutan; kemenangan (Victory) baru aktif setelah wave terakhir selesai.

## Catatan WebGL

- Semua script runtime memakai legacy Input Manager (`activeInputHandler: 0`), tanpa coroutine.
- Script editor dibungkus `#if UNITY_EDITOR` / folder `Editor/`, jadi tidak ikut ke build.
- Overlay `OnGUI` bersifat debug — matikan `showDebugOverlay` di `AlchemyManager` untuk build rapi,
  atau ganti dengan UI Canvas.
- Sprite placeholder dibuat uncompressed tanpa mipmap; ganti dengan art final bila diperlukan.
- `GameplayUIBridge` dan `ZombieIntroSpotlight` mendaftar otomatis lewat `RuntimeInitializeOnLoadMethod`,
  tetapi hanya aktif di scene gameplay (ada `WaveManager`/`AlchemyManager`) sehingga scene menu tidak tersentuh.
- Gunakan helper `UnityObjectUtil.Ensure()` untuk fallback referensi; jangan pakai `??` pada `UnityEngine.Object`
  karena operator itu melewati null-check khusus Unity.

## Verifikasi

- Kompilasi **0 error / 0 warning** untuk konfigurasi Editor dan Player pada Unity 2022.3.62f3.
- Geometri jalan diuji: 12 titik outline per jalur, tanpa self-intersection, semua waypoint di dalam
  polygon, polygon kiri/kanan tidak tumpang tindih.
- Logika pH & resep diuji: Asam −4.5, Basa +4.5, Pendingin/Katalis 0; kombinasi menghasilkan 2.5 /
  7.0 / 11.5 sesuai resep.