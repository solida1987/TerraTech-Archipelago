# Byggerapport — TerraTech Archipelago

**24. august 2026.** Bygget efter design v3. Intet er udgivet: repoet er
privat, og ingen release er lavet.

---

## Status pr. fase

| Fase | Hvad | Tilstand |
|---|---|---|
| 0 | Låse-beviset | **Kode færdig, ikke kørt i spillet** |
| 1 | Verden + blokke + bro | **Færdig og bevist** |
| 2 | AP-korps + butik | Kode-skelet, ikke wiret |
| 3 | Fjender + kister | Kode-skelet, ikke wiret |
| 4 | Quests + missioner | **Færdig** (tællere + missions-lokationer) |
| 5 | Slibning | Ikke påbegyndt |

Faserne 2 og 3 har deres kroge og datastrukturer på plads (`CarrierPools`,
`Rewards.DropCrate`), men kalder endnu ikke `CrateSpawner` og
`uScript_AddBlockToShopInventory`. De steder siger koden det selv, i stedet
for at lade som om.

---

## Det der er bevist

### Generering: 4 mål × 5 seeds × 4-spiller multiworld

```
full_licence  OK      collector  OK      ap_hunt  OK      minimal  OK
seed 1 · 42 · 777 · 2024 · 99999   —   alle 5 genererede
```

Det udgivne seed indeholder **2.988 linjer** med vores items og krydsplacering
mellem alle fire spillere.

### Porten: `tools/verify_build.py`

Syv kontroller, alle grønne — og **negativ-testet**: ændres ét enkelt blok-id
ud af 1.144 i C#-tabellen, fanger porten det.

| Kontrol | Hvorfor den findes |
|---|---|
| Python- og C#-tabellen er enige | Uenighed = item lander på en blok der ikke findes |
| Ingen `_deprecated_` blokke | Døde items ingen kan skaffe |
| Grad-fordelingen taber ingen lokationer | Afrunding = seed der ikke kan gennemføres |
| Vægtene matcher mellem verden og mod | Modden placerer bærere for lokationer der ikke findes |
| Python parser | — |
| `archipelago.json` findes | Krav fra AP 0.7.0 |
| Modden kompilerer | At læse C# beviser intet om at den bygger |

---

## Fejl fundet undervejs — og hvad de lærte

**1. Apworld'en kunne ikke læse sine egne data.**
`pathlib.Path(__file__).parent` virker fra kildekode og fejler inde i en
zippet apworld. Rettet til `pkgutil.get_data`. Det er præcis skellet mellem
"virker hos mig" og "virker når den er pakket".

**2. Jeg stillede en forkert diagnose og rettede alligevel noget rigtigt.**
Første generering hang i 10 minutter. Jeg konkluderede ydelsesproblem og
forudberegnede rolle-sættene. Den rigtige årsag var `Press enter to close` der
ventede på input. Forudberegningen er stadig korrekt og nødvendig — men jeg
skal ikke have credit for at have fundet den af den rigtige grund.

**3. Grad-lokationer for korporationer uden blokke.**
Regionerne fik alle 40 grad-lokationer, men items kun for korporationer i
puljen. Generering meldte 24 uopnåelige lokationer.

**4. 200 hjemløse items.**
"minimal" bad om 230 blok-licenser og tilbød 20 lokationer. Puljen tilpasses
nu verdenens størrelse i `generate_early`.

**5. Beskæringen fjernede alle våben.**
Da puljen blev skåret ned efter grad alene, forsvandt hvert eneste våben — og
"dræb 5 fjender" blev uopnåelig. Beskæringen garanterer nu mindst tre blokke
af hver logik-rolle. ⭐ Fundet af stress-testen, ikke ved at læse koden.

**6. En designfejl i min egen logik.**
Jeg krævede at grad-lokationer ventede på det forrige grad-*item*. Det er
forkert om spillet: grader tjenes med XP ved at spille, ikke ved at modtage
dem fra en anden verden. Fejlen byggede også en fem-dyb kæde som et lille seed
ikke kunne fylde. Grad-*itemet* gater stadig vores bærere; grad-*lokationen* er
spillerens egen fremgang.

**7. Budgettet kunne skære målet væk.**
`create_items` beskar grad-items for at få plads — og kunne ramme netop den
Grad 5 målet krævede. Et seed der genererede pænt og ikke kunne vindes.
Budgettet reserverer nu grad-items eksplicit, og hvis der alligevel skal skæres,
skæres der i licenser, aldrig i grader.

`★ Fem af de syv fejl producerede et seed der genererede uden at klage og ikke
kunne gennemføres. Ingen af dem ville være fundet ved at læse koden.`

---

## Arkitekturen som den blev

```
TerraTech (Mono)                          Archipelago
┌──────────────────────────┐             ┌────────────────────┐
│ TerraTechArchipelago.dll │             │ TerraTech Client   │
│  · BlockGate (låsen)     │◄─TCP 24601─►│  (i vores apworld) │
│  · Patches (Harmony)     │  JSON/linje │  · AP-sessionen    │
│  · CarrierPools          │             │  · items ind       │
│  · SlotState (sidefil)   │             │  · checks ud       │
└──────────────────────────┘             └────────────────────┘
```

**Låsen** er spillets egen: `TankBlock.LockBlockAttach()`, samme mekanik som
tutorialen bruger. En Harmony-postfix på `ManTechBuilder.CanBlockAttach` er
kontrolposten intet kan gå udenom.

**Ingen del af TerraTech ligger i repoet.** Modden slår alt op ved navn på
runtime (`Reflect.cs`), så en klon bygger uden at eje spillets assemblies. Hvis
en spil-opdatering flytter noget, siger modden det ved start og patcher ikke —
i stedet for at køre halvt.

---

## Tallene

| | |
|---|---|
| Blokke i spillets enum | 1.393 |
| Frasorteret (`_deprecated_`, vendors, kulisse) | 249 |
| **Spilbare blokke = items** | **1.144** |
| Korporationer med blokke | 8 |
| Lokationer, standard-seed | ~1.700 |
| Lokationer, fuld opsætning | ~2.500 |

⚠ Designet sagde 1.393. Det tal indeholdt 214 pensionerede blokke og 35
vendor-bygninger. **1.144** er det målte tal.

---

## Hvad der mangler før udgivelse

1. **Fase 0 i det kørende spil** — låsen, den røde farve, og at discover-alt
   faktisk åbner butikken. Alt er kode-bevist; intet er set.
2. **Blok-tabellens grader er *udledt*** af navnenes første ciffer. Modden skal
   eksportere den rigtige tabel fra `ManLicenses.GetBlockTier()` ved første
   kørsel, og `blocks.json` regenereres fra den.
3. Fase 2–3 wires (butik, fjender, kister).
4. Spilversionen pinnes mod en rigtig version i stedet for `1.4.x`.

**Ingen release, intet offentligt repo, ingen katalog-udgivelse** — kataloget
har manifestet lokalt og pluginet bygger, men er ikke pushet.
