# TRLM Manual Playtest Checklist — Sprint 06 / 07

MCP automation can verify that systems *function* (triggers fire, objectives
advance, numbers change) but cannot judge how the game actually *feels*. This
is a short list for Ömer to play through and give a gut read on. No wrong
answers — just note what stands out.

---

## MOVEMENT
- Does walking/sprinting feel responsive, or laggy/floaty?
- Is base walk speed too slow or about right for a slow survival game?
- Does crouch feel useful, or just slows you down for no reason?

## ROWING
- Does the SPACE-stroke rowing feel satisfying, or too spammy/twitchy?
- Does the boat feel too shaky/unstable on the waves?
- Is the diminishing-returns-on-spam effect noticeable, or invisible?

## WORLD / NAVIGATION
- Are the paths from coast → settlement → forest readable, or do you get lost?
- Does the forest feel too dense to navigate, or about right?
- Any spot where you get stuck on geometry?

## WOLF ENCOUNTER
- Does the wolf feel threatening, or is it easy to ignore?
- Too aggressive / not aggressive enough?
- **Be honest**: is the wolf's sliding movement (no rig yet — known issue)
  distracting enough to hurt the tension, or does it read fine at a glance?

## NIGHT
- Is night too dark to see anything, or well-balanced?
- Does the flashlight feel actually useful, or is it too weak/too strong?
- Does the atmosphere feel right for the survival tone?

## HUD
- Is the inventory panel (plain text list) readable at a glance?
- Does anything feel like it's cluttering the middle of the screen?
- Is the current objective notification noticeable without being annoying?

## PERFORMANCE
- Any visible stutter, especially entering the settlement or at night with
  the flashlight + fire both lit?
- Which region feels the worst, if any?
- (Automated testing measured ~15-19ms/frame post-optimization across 7
  locations — this checklist is to catch anything that measurement can't:
  stutter spikes, not just average frame time.)

## FULL LOOP FEEL
- Playing start to finish (neighborhood → boat → land → house → night →
  wolf → safe house → fire → sleep), does the pacing feel reasonable, or
  does any section drag?
- Any point where it wasn't clear what to do next?

---

## WEAPON FEEL (Sprint 07)
- Does the pistol's recoil feel satisfying, or too strong/too weak?
- Is reload too fast or too slow?
- Does aiming feel responsive?
- Is the shotgun powerful enough at close range to feel worth using?
- Does ammo feel valuable (i.e. do you think twice before firing), or
  do you have more than you need?
- **Be honest**: the weapons are primitive placeholder shapes (no 3D art
  exists yet) — does that break immersion badly, or is it tolerable given
  everything else already reads as a real environment?

## MELEE (Sprint 07)
- Does the knife feel dangerous to use, or too weak/too strong?
- Is the swing responsive, or does it feel laggy/unclear when it lands?

## INJURY (Sprint 07)
- Are the penalties (arm sway, leg slowdown, torso stamina drain) noticeable
  enough to matter, or too subtle to feel?
- Do they cross the line into annoying/unfair, or feel fair?
- Is it clear WHY you're suddenly swaying more or moving slower, or does it
  feel like a mystery debuff?
- Does the bleeding warning feel urgent enough to prompt you to bandage?

## EQUIPMENT WHEEL (Sprint 07)
- Does holding Tab feel fast to open and read, or fiddly?
- Is it clear what's equipped vs. available vs. empty?
- Does the game pausing while the wheel is open feel natural, or jarring?
