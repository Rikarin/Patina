from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_command_stack_add_channel import StackCommandStackAddChannel
  from ..models.stack_command_stack_add_instance import StackCommandStackAddInstance
  from ..models.stack_command_stack_add_layer import StackCommandStackAddLayer
  from ..models.stack_command_stack_add_mask_layer import StackCommandStackAddMaskLayer
  from ..models.stack_command_stack_duplicate_layers import StackCommandStackDuplicateLayers
  from ..models.stack_command_stack_flatten_layer import StackCommandStackFlattenLayer
  from ..models.stack_command_stack_move_layer import StackCommandStackMoveLayer
  from ..models.stack_command_stack_move_mask_layer import StackCommandStackMoveMaskLayer
  from ..models.stack_command_stack_nest_layer import StackCommandStackNestLayer
  from ..models.stack_command_stack_paste_layers import StackCommandStackPasteLayers
  from ..models.stack_command_stack_remove_layer import StackCommandStackRemoveLayer
  from ..models.stack_command_stack_remove_mask_layer import StackCommandStackRemoveMaskLayer
  from ..models.stack_command_stack_set_height_normal import StackCommandStackSetHeightNormal
  from ..models.stack_command_stack_set_instance import StackCommandStackSetInstance
  from ..models.stack_command_stack_set_layer import StackCommandStackSetLayer
  from ..models.stack_command_stack_set_mask_layer import StackCommandStackSetMaskLayer
  from ..models.stack_command_stack_set_model import StackCommandStackSetModel
  from ..models.stack_command_stack_set_seed import StackCommandStackSetSeed
  from ..models.stack_command_stack_stroke import StackCommandStackStroke
  from ..models.stack_command_stack_unflatten_layer import StackCommandStackUnflattenLayer





T = TypeVar("T", bound="StackCommandBatch")



@_attrs_define
class StackCommandBatch:
    """ Edits applied whole or not at all, as one undo step.

        Attributes:
            commands (list[StackCommandStackAddChannel | StackCommandStackAddInstance | StackCommandStackAddLayer |
                StackCommandStackAddMaskLayer | StackCommandStackDuplicateLayers | StackCommandStackFlattenLayer |
                StackCommandStackMoveLayer | StackCommandStackMoveMaskLayer | StackCommandStackNestLayer |
                StackCommandStackPasteLayers | StackCommandStackRemoveLayer | StackCommandStackRemoveMaskLayer |
                StackCommandStackSetHeightNormal | StackCommandStackSetInstance | StackCommandStackSetLayer |
                StackCommandStackSetMaskLayer | StackCommandStackSetModel | StackCommandStackSetSeed | StackCommandStackStroke |
                StackCommandStackUnflattenLayer]): The edits, in order.
            label (None | str | Unset): What the undo step says.
     """

    commands: list[StackCommandStackAddChannel | StackCommandStackAddInstance | StackCommandStackAddLayer | StackCommandStackAddMaskLayer | StackCommandStackDuplicateLayers | StackCommandStackFlattenLayer | StackCommandStackMoveLayer | StackCommandStackMoveMaskLayer | StackCommandStackNestLayer | StackCommandStackPasteLayers | StackCommandStackRemoveLayer | StackCommandStackRemoveMaskLayer | StackCommandStackSetHeightNormal | StackCommandStackSetInstance | StackCommandStackSetLayer | StackCommandStackSetMaskLayer | StackCommandStackSetModel | StackCommandStackSetSeed | StackCommandStackStroke | StackCommandStackUnflattenLayer]
    label: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_command_stack_add_channel import StackCommandStackAddChannel # noqa: PLC0415
        from ..models.stack_command_stack_add_instance import StackCommandStackAddInstance # noqa: PLC0415
        from ..models.stack_command_stack_add_layer import StackCommandStackAddLayer # noqa: PLC0415
        from ..models.stack_command_stack_add_mask_layer import StackCommandStackAddMaskLayer # noqa: PLC0415
        from ..models.stack_command_stack_duplicate_layers import StackCommandStackDuplicateLayers # noqa: PLC0415
        from ..models.stack_command_stack_flatten_layer import StackCommandStackFlattenLayer # noqa: PLC0415
        from ..models.stack_command_stack_move_layer import StackCommandStackMoveLayer # noqa: PLC0415
        from ..models.stack_command_stack_move_mask_layer import StackCommandStackMoveMaskLayer # noqa: PLC0415
        from ..models.stack_command_stack_nest_layer import StackCommandStackNestLayer # noqa: PLC0415
        from ..models.stack_command_stack_paste_layers import StackCommandStackPasteLayers # noqa: PLC0415
        from ..models.stack_command_stack_remove_layer import StackCommandStackRemoveLayer # noqa: PLC0415
        from ..models.stack_command_stack_remove_mask_layer import StackCommandStackRemoveMaskLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_height_normal import StackCommandStackSetHeightNormal # noqa: PLC0415
        from ..models.stack_command_stack_set_instance import StackCommandStackSetInstance # noqa: PLC0415
        from ..models.stack_command_stack_set_layer import StackCommandStackSetLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_mask_layer import StackCommandStackSetMaskLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_model import StackCommandStackSetModel # noqa: PLC0415
        from ..models.stack_command_stack_set_seed import StackCommandStackSetSeed # noqa: PLC0415
        from ..models.stack_command_stack_stroke import StackCommandStackStroke # noqa: PLC0415
        from ..models.stack_command_stack_unflatten_layer import StackCommandStackUnflattenLayer # noqa: PLC0415
        commands = []
        for commands_item_data in self.commands:
            commands_item: dict[str, Any]
            if isinstance(commands_item_data, StackCommandStackAddLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackRemoveLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackMoveLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackSetLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackStroke):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackSetModel):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackAddChannel):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackNestLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackSetSeed):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackDuplicateLayers):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackPasteLayers):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackFlattenLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackUnflattenLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackSetHeightNormal):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackAddMaskLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackRemoveMaskLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackMoveMaskLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackSetMaskLayer):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, StackCommandStackAddInstance):
                commands_item = commands_item_data.to_dict()
            else:
                commands_item = commands_item_data.to_dict()

            commands.append(commands_item)



        label: None | str | Unset
        if isinstance(self.label, Unset):
            label = UNSET
        else:
            label = self.label


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "commands": commands,
        })
        if label is not UNSET:
            field_dict["label"] = label

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_command_stack_add_channel import StackCommandStackAddChannel # noqa: PLC0415
        from ..models.stack_command_stack_add_instance import StackCommandStackAddInstance # noqa: PLC0415
        from ..models.stack_command_stack_add_layer import StackCommandStackAddLayer # noqa: PLC0415
        from ..models.stack_command_stack_add_mask_layer import StackCommandStackAddMaskLayer # noqa: PLC0415
        from ..models.stack_command_stack_duplicate_layers import StackCommandStackDuplicateLayers # noqa: PLC0415
        from ..models.stack_command_stack_flatten_layer import StackCommandStackFlattenLayer # noqa: PLC0415
        from ..models.stack_command_stack_move_layer import StackCommandStackMoveLayer # noqa: PLC0415
        from ..models.stack_command_stack_move_mask_layer import StackCommandStackMoveMaskLayer # noqa: PLC0415
        from ..models.stack_command_stack_nest_layer import StackCommandStackNestLayer # noqa: PLC0415
        from ..models.stack_command_stack_paste_layers import StackCommandStackPasteLayers # noqa: PLC0415
        from ..models.stack_command_stack_remove_layer import StackCommandStackRemoveLayer # noqa: PLC0415
        from ..models.stack_command_stack_remove_mask_layer import StackCommandStackRemoveMaskLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_height_normal import StackCommandStackSetHeightNormal # noqa: PLC0415
        from ..models.stack_command_stack_set_instance import StackCommandStackSetInstance # noqa: PLC0415
        from ..models.stack_command_stack_set_layer import StackCommandStackSetLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_mask_layer import StackCommandStackSetMaskLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_model import StackCommandStackSetModel # noqa: PLC0415
        from ..models.stack_command_stack_set_seed import StackCommandStackSetSeed # noqa: PLC0415
        from ..models.stack_command_stack_stroke import StackCommandStackStroke # noqa: PLC0415
        from ..models.stack_command_stack_unflatten_layer import StackCommandStackUnflattenLayer # noqa: PLC0415
        d = dict(src_dict)
        commands = []
        _commands = d.pop("commands")
        for commands_item_data in (_commands):
            def _parse_commands_item(data: object) -> StackCommandStackAddChannel | StackCommandStackAddInstance | StackCommandStackAddLayer | StackCommandStackAddMaskLayer | StackCommandStackDuplicateLayers | StackCommandStackFlattenLayer | StackCommandStackMoveLayer | StackCommandStackMoveMaskLayer | StackCommandStackNestLayer | StackCommandStackPasteLayers | StackCommandStackRemoveLayer | StackCommandStackRemoveMaskLayer | StackCommandStackSetHeightNormal | StackCommandStackSetInstance | StackCommandStackSetLayer | StackCommandStackSetMaskLayer | StackCommandStackSetModel | StackCommandStackSetSeed | StackCommandStackStroke | StackCommandStackUnflattenLayer:
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_0 = StackCommandStackAddLayer.from_dict(data)



                    return componentsschemas_stack_command_type_0
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_1 = StackCommandStackRemoveLayer.from_dict(data)



                    return componentsschemas_stack_command_type_1
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_2 = StackCommandStackMoveLayer.from_dict(data)



                    return componentsschemas_stack_command_type_2
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_3 = StackCommandStackSetLayer.from_dict(data)



                    return componentsschemas_stack_command_type_3
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_4 = StackCommandStackStroke.from_dict(data)



                    return componentsschemas_stack_command_type_4
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_5 = StackCommandStackSetModel.from_dict(data)



                    return componentsschemas_stack_command_type_5
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_6 = StackCommandStackAddChannel.from_dict(data)



                    return componentsschemas_stack_command_type_6
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_7 = StackCommandStackNestLayer.from_dict(data)



                    return componentsschemas_stack_command_type_7
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_8 = StackCommandStackSetSeed.from_dict(data)



                    return componentsschemas_stack_command_type_8
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_9 = StackCommandStackDuplicateLayers.from_dict(data)



                    return componentsschemas_stack_command_type_9
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_10 = StackCommandStackPasteLayers.from_dict(data)



                    return componentsschemas_stack_command_type_10
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_11 = StackCommandStackFlattenLayer.from_dict(data)



                    return componentsschemas_stack_command_type_11
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_12 = StackCommandStackUnflattenLayer.from_dict(data)



                    return componentsschemas_stack_command_type_12
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_13 = StackCommandStackSetHeightNormal.from_dict(data)



                    return componentsschemas_stack_command_type_13
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_14 = StackCommandStackAddMaskLayer.from_dict(data)



                    return componentsschemas_stack_command_type_14
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_15 = StackCommandStackRemoveMaskLayer.from_dict(data)



                    return componentsschemas_stack_command_type_15
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_16 = StackCommandStackMoveMaskLayer.from_dict(data)



                    return componentsschemas_stack_command_type_16
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_17 = StackCommandStackSetMaskLayer.from_dict(data)



                    return componentsschemas_stack_command_type_17
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_stack_command_type_18 = StackCommandStackAddInstance.from_dict(data)



                    return componentsschemas_stack_command_type_18
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                if not isinstance(data, dict):
                    raise TypeError()
                componentsschemas_stack_command_type_19 = StackCommandStackSetInstance.from_dict(data)



                return componentsschemas_stack_command_type_19

            commands_item = _parse_commands_item(commands_item_data)

            commands.append(commands_item)


        def _parse_label(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        label = _parse_label(d.pop("label", UNSET))


        stack_command_batch = cls(
            commands=commands,
            label=label,
        )


        stack_command_batch.additional_properties = d
        return stack_command_batch

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
