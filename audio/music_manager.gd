extends Node
## AdaptiveMusicManager — Godot 4.x autoload singleton.
##
## Three parallel music channels driven by game state:
##   1. THEME      — main ambient theme, always playing.
##   2. CORRUPTION — two stems derived from the theme: a bass-heavy "dark"
##                   variant and a treble-heavy "eerie" variant. Blended by a
##                   corruption value in [-1, 1]: sign picks the flavour,
##                   magnitude picks the intensity. 0 = clean theme only.
##   3. COMBAT     — danger layer that fades in on encounters (optionally
##                   quantized to the next beat) and ducks the other channels.
##
## All stems must share the same BPM, key and length, with looping enabled
## in their import settings. See godot_adaptive_music_setup.md for the
## full workflow (including how to generate the stems).

signal combat_started
signal combat_ended

# ---------------------------------------------------------------- streams ---

@export_group("Streams")
## Main ambient theme (channel 1). Loops forever.
@export var theme_stream: AudioStream
## Corruption variant — bass-heavy / oppressive (channel 2, negative side).
@export var corruption_dark_stream: AudioStream
## Corruption variant — treble-harsh / eerie (channel 2, positive side).
@export var corruption_eerie_stream: AudioStream
## Encounter / danger layer (channel 3).
@export var combat_stream: AudioStream

# ----------------------------------------------------------------- tuning ---

@export_group("Timing")
## BPM shared by all stems. Used to quantize combat entry to the beat.
@export var bpm: float = 90.0
## If true, the combat layer starts on the next beat of the theme.
@export var combat_quantize_to_beat: bool = true
## Seconds for the corruption blend to travel the full 0 -> 1 range.
@export var corruption_smoothing: float = 1.5
@export var combat_fade_in: float = 0.8
@export var combat_fade_out: float = 1.2

@export_group("Mix")
## How much the theme + corruption channels are ducked during combat, in dB.
@export var duck_db: float = -6.0
## Optional: extra pitch shift applied on the dark corruption bus, in
## semitones (e.g. -2.0 subtly detunes the dark layer). 0 disables it.
@export var dark_bus_pitch_semitones: float = 0.0

# ---------------------------------------------------------------- internal ---

const BUS_MUSIC := "Music"
const BUS_THEME := "MTheme"
const BUS_DARK := "MDark"
const BUS_EERIE := "MEerie"
const BUS_COMBAT := "MCombat"
const SILENCE_DB := -60.0

var _theme_player: AudioStreamPlayer
var _dark_player: AudioStreamPlayer
var _eerie_player: AudioStreamPlayer
var _combat_player: AudioStreamPlayer

var _corruption_target: float = 0.0
var _corruption_current: float = 0.0

var _in_combat: bool = false
var _combat_tween: Tween
var _duck_tween: Tween


func _ready() -> void:
	_ensure_buses()
	_build_players()
	_start_all_stems()


# ------------------------------------------------------------------- API ----

## Set corruption in [-1, 1]. Negative = bass/dark flavour, positive =
## treble/eerie flavour, magnitude = intensity. Smoothed over
## corruption_smoothing seconds, so call it as often as you like.
func set_corruption(value: float) -> void:
	_corruption_target = clampf(value, -1.0, 1.0)


func enter_combat() -> void:
	if _in_combat:
		return
	_in_combat = true
	_apply_duck(true)
	if combat_quantize_to_beat:
		_start_combat_on_next_beat()
	else:
		_fade_combat_to(1.0, combat_fade_in)
	combat_started.emit()


func exit_combat() -> void:
	if not _in_combat:
		return
	_in_combat = false
	_apply_duck(false)
	_fade_combat_to(0.0, combat_fade_out)
	combat_ended.emit()


# ---------------------------------------------------------------- process ---

func _process(delta: float) -> void:
	if is_equal_approx(_corruption_current, _corruption_target):
		return
	var rate := delta / maxf(corruption_smoothing, 0.01)
	_corruption_current = move_toward(_corruption_current, _corruption_target, rate)
	_apply_corruption_blend(_corruption_current)


# ------------------------------------------------------------- corruption ---

## Equal-power-ish blend across the three stems. Gains sum to <= 1.
func _apply_corruption_blend(c: float) -> void:
	var intensity := absf(c)
	var clean_gain := 1.0 - intensity
	var dark_gain := maxf(-c, 0.0)
	var eerie_gain := maxf(c, 0.0)
	_set_player_gain(_theme_player, clean_gain)
	_set_player_gain(_dark_player, dark_gain)
	_set_player_gain(_eerie_player, eerie_gain)


func _set_player_gain(player: AudioStreamPlayer, linear_gain: float) -> void:
	# sqrt softens the crossfade midpoint so the mix doesn't dip.
	var shaped := sqrt(clampf(linear_gain, 0.0, 1.0))
	player.volume_db = linear_to_db(maxf(shaped, 0.0001))
	if shaped <= 0.0001:
		player.volume_db = SILENCE_DB


# ----------------------------------------------------------------- combat ---

func _start_combat_on_next_beat() -> void:
	var beat_len := 60.0 / bpm
	var pos := _theme_player.get_playback_position()
	var wait := beat_len - fposmod(pos, beat_len)
	if wait < 0.02:
		_fade_combat_to(1.0, combat_fade_in)
		return
	# Park the combat player at full target volume only after the beat hits.
	await get_tree().create_timer(wait).timeout
	if _in_combat:  # combat may have ended while we waited
		_fade_combat_to(1.0, combat_fade_in)


func _fade_combat_to(target_gain: float, duration: float) -> void:
	if _combat_tween and _combat_tween.is_valid():
		_combat_tween.kill()
	if target_gain > 0.0 and not _combat_player.playing:
		_combat_player.volume_db = SILENCE_DB
		_combat_player.play()
	_combat_tween = create_tween()
	var target_db := SILENCE_DB if target_gain <= 0.0 else linear_to_db(target_gain)
	_combat_tween.tween_property(_combat_player, "volume_db", target_db, duration)
	if target_gain <= 0.0:
		_combat_tween.tween_callback(_combat_player.stop)


## Duck theme + corruption buses while combat is active.
func _apply_duck(active: bool) -> void:
	if _duck_tween and _duck_tween.is_valid():
		_duck_tween.kill()
	_duck_tween = create_tween().set_parallel(true)
	for bus_name in [BUS_THEME, BUS_DARK, BUS_EERIE]:
		var idx := AudioServer.get_bus_index(bus_name)
		var target := duck_db if active else 0.0
		_duck_tween.tween_method(
			func(v): AudioServer.set_bus_volume_db(idx, v),
			AudioServer.get_bus_volume_db(idx), target, combat_fade_in)


# ----------------------------------------------------------------- setup ----

func _ensure_buses() -> void:
	_add_bus_if_missing(BUS_MUSIC, "Master")
	for b in [BUS_THEME, BUS_DARK, BUS_EERIE, BUS_COMBAT]:
		_add_bus_if_missing(b, BUS_MUSIC)
	if not is_zero_approx(dark_bus_pitch_semitones):
		var idx := AudioServer.get_bus_index(BUS_DARK)
		var fx := AudioEffectPitchShift.new()
		fx.pitch_scale = pow(2.0, dark_bus_pitch_semitones / 12.0)
		AudioServer.add_bus_effect(idx, fx)


func _add_bus_if_missing(bus_name: String, send_to: String) -> void:
	if AudioServer.get_bus_index(bus_name) != -1:
		return
	AudioServer.add_bus()
	var idx := AudioServer.bus_count - 1
	AudioServer.set_bus_name(idx, bus_name)
	AudioServer.set_bus_send(idx, send_to)


func _build_players() -> void:
	_theme_player = _make_player(theme_stream, BUS_THEME)
	_dark_player = _make_player(corruption_dark_stream, BUS_DARK)
	_eerie_player = _make_player(corruption_eerie_stream, BUS_EERIE)
	_combat_player = _make_player(combat_stream, BUS_COMBAT)


func _make_player(stream: AudioStream, bus_name: String) -> AudioStreamPlayer:
	var p := AudioStreamPlayer.new()
	p.stream = stream
	p.bus = bus_name
	add_child(p)
	return p


## Start every stem at the same frame so they stay sample-aligned.
## Corruption stems start silent; combat starts stopped.
func _start_all_stems() -> void:
	_apply_corruption_blend(0.0)
	_combat_player.volume_db = SILENCE_DB
	for p in [_theme_player, _dark_player, _eerie_player]:
		if p.stream:
			p.play()
