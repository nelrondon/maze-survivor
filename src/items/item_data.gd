class_name ItemData extends Resource

enum ItemType { CONSUMABLE, INTERACTABLE, WEAPON }

@export var id: String = ""
@export var display_name: String = ""
@export var description: String = ""
@export var stackable: bool = false
@export var item_type: ItemType
@export var use_sound: AudioStream
@export var icon: Texture2D
@export var view_model: PackedScene
