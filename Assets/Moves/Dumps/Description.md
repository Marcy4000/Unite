`databin_Active_Skill_Hero.json` is a decompiled client-side configuration database (databin) from *Pokémon UNITE*, built on Tencent TiMi Studio's MOBA framework. It defines the parameters for every active ability usable by playable Pokémon ("Heroes"), including basic attacks, base moves, branched move upgrades, "+" move variants, Unite Moves, and system placeholder states (such as locked skill slots or empty held-item slots). While another table handles actual damage formulas and stat scaling, this file governs how skills behave in the game engine: their cooldowns, targeting rules, range indicators, camera panning, animation triggers, and AI logic.

---

### 1. Identification, Localization & UI Assets

These properties link the skill to its internal ID, text strings, and visual icons in the user interface.

* **`SkillId`**: The unique integer identifier for the skill. It follows a structured naming convention: a prefix (`93` or `3`), followed by the 3-digit National Pokédex number (e.g., `007` for Squirtle), a slot/branch identifier (`00`–`05` for basic/boosted attacks, `10`/`11`/`12` for Skill 1 branches, `20`/`21`/`22` for Skill 2 branches, `30`/`31`/`32` for alternative slots, `40` for Unite Moves), and trailing digits (`1`, `11`, `12`) for `+` upgrades or secondary activation phases.


* **`Name` / `Desc` / `SimpleDesc**`: Localization table keys (e.g., `Pkm_SkillName_WaterGun_001`, `Pkm_SkillDes_2_Squirtle_001`) that fetch the translated move name, full description, and simplified tooltip text.


* **`DescParameter`**: An array of 10 integers used to inject dynamic values (like cooldowns or damage numbers) into placeholders within the localization description string.


* **`IconPath` / `NSIconPath**`: Asset paths for the 2D skill button icon displayed on mobile/standard HUDs (`IconPath`) and Nintendo Switch HUDs (`NSIconPath`).


* **`EffectIconPath` / `EffectLabel` / `SkillLogo**`: UI category tags indicating what kind of move it is on the selection screen (e.g., `t_Skill_Label_dandao` for ranged/projectile, `t_Skill_Label_fanwei` for Area of Effect, paired with `SkillLogo` integer IDs like `[3, 0]` or `[2, 0]`).


* **`PreviewPicPath`**: The asset path for the miniature video or image preview shown in the in-game move-selection popup when leveling up.


* **`HideCancelBtn`**: A boolean flag (`0` or `1`) that hides the "Cancel Skill" UI button while aiming.



---

### 2. Skill Classification, Progression & Slot Logic

These fields dictate how a move fits into a Pokémon's leveling tree, how it upgrades, and how it interacts with other skill slots.

* **`SkillBaseType` / `DamageType` / `SkillEffectType` / `SpecialSkillType` / `USkillType**`: Enumerated integers classifying the skill's fundamental nature (e.g., `SkillBaseType` distinguishes physical `0`, special `1`, status `2`, and system skills `3`; `USkillType` flags boosted basic attack variations).


* **`SkillSlotLevelUpGroupId`**: Groups a base move, its branched evolutions, and its `+` version together (e.g., `9300711` groups Squirtle's Water Spout and Water Spout+).


* **`PreSkillId` / `StrengthenSkillId` / `ReinforceSkill**`: Defines prerequisite skills required before learning this move (`PreSkillId`) or links to the empowered/strengthened version of the move.


* **`IsPlusSkill`**: Boolean flag (`1` or `0`) indicating whether this entry is the upgraded `+` version unlocked at higher levels.


* **`IsGSkill`**: Boolean flag (`1` or `0`) marking the move as a Unite Move ("G-Skill" internally).


* **`IgnoreSkillLevel`**: Flag determining if the skill's scaling ignores the Pokémon's current level.


* **`CoexistSkill` / `CoexistPriority**`: Controls whether this skill can be cast simultaneously while another skill is active, and which takes precedence.


* **`IgnoreForbidSkill` / `CancelPressWhenForbid**`: Determines if the skill can be cast while the Pokémon is crowd-controlled (silenced/stunned), or if holding the button down gets canceled when hit by crowd control.


* **`ForbidNormalAtkWithoutTarget` / `NormalAtkKeepEmptyAtkRange**`: Controls whether the player can swing/fire basic attacks into empty air without a valid target in range.


* **`MultiformSpecialID` / `MultiformPokmeonID**`: Used for form-changing Pokémon (such as Mew, Hoopa, Scyther/Scizor, or Urshifu) to link skills to specific forms. (Note the developer typo `Pokmeon` in the schema).



---

### 3. Cooldown, Energy & Charge (Store) Mechanics

In TiMi's engine, time is measured in **milliseconds**. These properties manage how often a skill can be used and how held/charged moves function.

* **`CDTime`**: The base cooldown of the skill in milliseconds (e.g., `6000` = 6.0 seconds, `30000` = 30.0 seconds).


* **`IsCDIndependent` / `SkillCDEffectInvalid**`: Flags whether the skill has an independent cooldown timer (often `1` for secondary phases of a 2-part move) or if it ignores cooldown reduction (CDR) buffs.


* **`CostEnergyType` / `EnergyCostVal` / `EnergyCostValCalcType**`: Defines the resource required to cast the move. Regular cooldown moves use `CostEnergyType: 1` and `EnergyCostVal: 0`. Unite Moves use `CostEnergyType: 4` and an `EnergyCostVal` like `90000` or `120000` (representing the total Unite Gauge energy units needed to reach 100%).


* **`FloatWordNotEnoughEnergyText`**: Localization key for the floating error text shown if the player tries to cast without enough energy/Unite charge.


* **`SkillBeanId`**: Links to ammo/charge-stock systems ("Beans" in TiMi's engine) for moves that store multiple uses.


* **`StoreStartEffect` / `StoreEndEffect` / `StoreTimeLimit` / `StoreReleaseTime` / `StoreCancelCD` / `StoreStartRandomRadius` / `StoreEndRandomRadius` / `ChargingRangeMagnification**`: Controls hold-to-charge or channeled moves (like Squirtle's Water Spout during Rapid Spin, which has a `StoreTimeLimit` of `5000` ms). It defines the buff/effect IDs triggered when charging starts and ends, maximum hold duration, cooldown penalty if canceled early, and how much the aiming indicator grows while charging.



---

### 4. Range, Targeting & Smart Select Filters

Distances in this engine are measured in **millimeters** (1000 units = 1.0 meter in-game). These fields govern who the skill can hit and how auto-aim selects targets.

* **`AffectRange` / `AffectLocation` / `ParabolaHeight**`: The size/width/radius of the skill's hitbox (`AffectRange`, e.g., `2500` = 2.5m radius), its 3D positional offset (`AffectLocation`), and the arc height (`ParabolaHeight`) if it is a lobbed projectile.


* **`MaxAttackDis` / `SecondAttackDis` / `MaxSearchDis` / `CloseSecondAttackDis` / `CloseMaxSearchDis` / `MaxFollowDis**`: Defines the engagement and pursuit radii. `MaxAttackDis` is the maximum cast range; `MaxSearchDis` is the radius the game searches for a valid target when you tap the button; `MaxFollowDis` (`12500` = 12.5m) is how far your Pokémon will automatically walk to chase a target if you press attack while out of range.


* **`RangeAppointType` / `RangeAppointSubType**`: Defines the targeting paradigm: `1` = unit lock-on (e.g., Charmander's Unite Move), `2` = directional skillshot/line, `3` = ground-targeted AoE circle, `4` = auto-targeted basic attack.


* **`SkillTargetFilter` / `SkillDoRule` / `SkillFilterRule` / `CampPriorityType**`: Bitmasks and enums dictating valid targets (e.g., enemies vs. allies, wild Pokémon vs. enemy players) and how the skill executes.


* **`ObjContainSelf` / `ObjContainEnemy**`: Boolean flags specifying whether the skill's effect can target the caster (`ObjContainSelf`) and/or opposing units (`ObjContainEnemy: 1`).


* **`SkillSmartSelectType` / `SmartSelectFilterMask` / `SmartSelectNotIncludeEquivalent` / `UseTargetSelectSettings**`: Configures the auto-aim ("Smart Select") algorithm (e.g., `2334718` is the standard bitmask for offensive auto-aim), determining whether the move respects the player's "Lowest HP% / Lowest HP Value / Closest" targeting settings.



---

### 5. Aiming Indicators (Reticles)

These properties control the visual aiming guides projected on the ground or minimap when holding down a skill button.

* **`SkillIndicatePath` / `SkillAffectPath` / `MiniMapSkillIndicatorPath**`: Prefab paths for the outer range circle (`pref_Indicator_Circle_Auto`), the inner directional arrow or AoE reticle (`pref_Indicator_Arrow`, `pref_Indicator_Circle`, `pref_Indicator_Lock_90`), and long-range minimap indicators.


* **`IndicatorRange` / `IndicatorAngle` / `IndicatorReboundRange**`: The length/radius of the aiming reticle (`IndicatorRange`), cone angle (`IndicatorAngle`), and bounce distance for ricochet skills.


* **`IndicatorAnimSpeedBuffId` / `IndicatorAnimSpeedParam**`: Adjusts how fast animated aiming reticles expand or move based on active buffs.


* **`TargetInRangeUseSpecialIndicator` / `SpecialIndicatorScale` / `HideLockIndicator**`: Modifies or hides the reticle when a valid target steps inside the aiming zone.


* **`IndicatorSmartOffsetLength` / `IndicatorSmartOffsetAngle` / `SmartIndicatorDir` / `SmartIndicatorTarget**`: Controls how the reticle snaps toward targets when auto-aim initializes.


* **`SkillCursorScale` / `NSDragSensitivity**`: Adjusts the aiming cursor size and right-stick drag sensitivity on Nintendo Switch.


* **`IgnoreIndicatorOPos` / `IndicatorStickToGround**`: Determines whether the reticle stays anchored to the caster's origin and whether it conforms to terrain elevation.



---

### 6. Execution, Animations (AGE) & Combat Effects

These fields connect the skill data to the game's animation engine and combat effect tables.

* **`AGEActionPath` / `AgeExecuteImm**`: **AGE** (*Action Game Engine*) is TiMi Studio's timeline-based animation and hitbox scripting system (originally built for *Honor of Kings*). `AGEActionPath` points to the script file (e.g., `age_new_007_S1` for Squirtle's Water Gun) that controls frame data, particle spawns, and sound effects. Entries with `"age_DoNothing"` are either system placeholders or stripped/server-handled actions. `AgeExecuteImm` (`1` or `0`) dictates whether the animation script fires instantaneously bypassing normal cast queues.


* **`RefEffectGroupIds`**: An array of up to 4 IDs (e.g., `[9007100, 9007109, 0, 0]`) that reference external skill-effect and buff tables where the actual damage ratios, crowd-control durations, and stat changes are stored.


* **`SelectBuffId`**: A specific buff ID required on a target or caster to select/use the skill.


* **`AtkSpdNotAnim` / `AtkSpdNotAtkInter**`: Boolean flags (`0` or `1`) specifying that attack speed buffs should *not* speed up the move's animation (`AtkSpdNotAnim`) or reduce the interval between attacks (`AtkSpdNotAtkInter`).


* **`MakeDamageAsOnce` / `MakeDamageAsSearchTarget` / `NoDefaultUseSkillSuccess` / `DeathReviewDamgeStatBelongSkillId**`: Combat bookkeeping flags. `MakeDamageAsOnce` treats multi-hit/ticking moves as a single damage instance; `DeathReviewDamgeStatBelongSkillId` attributes damage from sub-skills or summoned projectiles back to the main parent skill on the player's post-death recap screen.



---

### 7. Camera Control

When aiming long-range moves (like Venusaur's Solar Beam or Talonflame's Brave Bird), the camera pans away from the player. These properties configure that behavior.

* **`CameraFollowIndicator` / `CameraStartFollowDis` / `CameraFollowIndicatorHeight**`: Controls whether the camera tracks the aiming reticle, at what distance it starts panning, and the camera's vertical height offset (`1000`).


* **`CameraOffsetBySkillBtn` / `CameraOffsetBySkillBtnRangeMin` / `CameraOffsetBySkillBtnRangeMax` / `CameraOffsetBySkillBtnSpeed` / `CameraOffsetBySkillBtnDuration**`: Enables camera panning when holding the skill button (`CameraOffsetBySkillBtn: 1`) and sets the minimum/maximum pan distance (matching `IndicatorRange`), pan speed, and duration.


* **`CameraOffsetBySkillBtnLerpBackDelay` / `CameraOffsetBySkillBtnLerpBackTime` / `IsForceCameraOffsetBySkillBtnLerpBackDelay**`: Configures the delay (e.g., `750` ms) and interpolation ("lerp") speed for the camera to smoothly snap back to your Pokémon after releasing the skill button.



---

### 8. AI & Bot Behavior (Behaviac)

*Pokémon UNITE* uses Tencent's open-source **Behaviac** framework (a C++ behavior tree engine) to control bots, wild Pokémon, and AFK-replacement AI.

* **`SkillBehaviacId`**: The behavior tree node ID (`10000001`) that executes this skill's AI logic.


* **`BehaviacEvoluation`**: A priority weight (typically `100` for standard moves and `300` for Unite Moves) used by the AI to evaluate which skill is most valuable to cast when multiple moves are off cooldown. (Note the developer typo `Evoluation` for "Evaluation").


* **`AISkillType` / `AISearchMoveStop` / `ShowSkillInAIMoveIndicator` / `SkillRandomOffsetMove` / `SearchId**`: Fine-tunes how bots aim and move while casting, such as whether a bot stops moving when searching for a target (`AISearchMoveStop`) or how much artificial aiming variance/movement offset (`SkillRandomOffsetMove`) is applied.
