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
  from ..models.stack_command_stack_add_layer import StackCommandStackAddLayer
  from ..models.stack_command_stack_move_layer import StackCommandStackMoveLayer
  from ..models.stack_command_stack_nest_layer import StackCommandStackNestLayer
  from ..models.stack_command_stack_remove_layer import StackCommandStackRemoveLayer
  from ..models.stack_command_stack_set_layer import StackCommandStackSetLayer
  from ..models.stack_command_stack_set_model import StackCommandStackSetModel
  from ..models.stack_command_stack_set_seed import StackCommandStackSetSeed
  from ..models.stack_command_stack_stroke import StackCommandStackStroke





T = TypeVar("T", bound="StackCommandBatch")



@_attrs_define
class StackCommandBatch:
    """ Edits applied whole or not at all, as one undo step.

        Attributes:
            commands (list[StackCommandStackAddChannel | StackCommandStackAddLayer | StackCommandStackMoveLayer |
                StackCommandStackNestLayer | StackCommandStackRemoveLayer | StackCommandStackSetLayer |
                StackCommandStackSetModel | StackCommandStackSetSeed | StackCommandStackStroke]): The edits, in order.
            label (None | str | Unset): What the undo step says.
     """

    commands: list[StackCommandStackAddChannel | StackCommandStackAddLayer | StackCommandStackMoveLayer | StackCommandStackNestLayer | StackCommandStackRemoveLayer | StackCommandStackSetLayer | StackCommandStackSetModel | StackCommandStackSetSeed | StackCommandStackStroke]
    label: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_command_stack_add_channel import StackCommandStackAddChannel # noqa: PLC0415
        from ..models.stack_command_stack_add_layer import StackCommandStackAddLayer # noqa: PLC0415
        from ..models.stack_command_stack_move_layer import StackCommandStackMoveLayer # noqa: PLC0415
        from ..models.stack_command_stack_nest_layer import StackCommandStackNestLayer # noqa: PLC0415
        from ..models.stack_command_stack_remove_layer import StackCommandStackRemoveLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_layer import StackCommandStackSetLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_model import StackCommandStackSetModel # noqa: PLC0415
        from ..models.stack_command_stack_set_seed import StackCommandStackSetSeed # noqa: PLC0415
        from ..models.stack_command_stack_stroke import StackCommandStackStroke # noqa: PLC0415
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
        from ..models.stack_command_stack_add_layer import StackCommandStackAddLayer # noqa: PLC0415
        from ..models.stack_command_stack_move_layer import StackCommandStackMoveLayer # noqa: PLC0415
        from ..models.stack_command_stack_nest_layer import StackCommandStackNestLayer # noqa: PLC0415
        from ..models.stack_command_stack_remove_layer import StackCommandStackRemoveLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_layer import StackCommandStackSetLayer # noqa: PLC0415
        from ..models.stack_command_stack_set_model import StackCommandStackSetModel # noqa: PLC0415
        from ..models.stack_command_stack_set_seed import StackCommandStackSetSeed # noqa: PLC0415
        from ..models.stack_command_stack_stroke import StackCommandStackStroke # noqa: PLC0415
        d = dict(src_dict)
        commands = []
        _commands = d.pop("commands")
        for commands_item_data in (_commands):
            def _parse_commands_item(data: object) -> StackCommandStackAddChannel | StackCommandStackAddLayer | StackCommandStackMoveLayer | StackCommandStackNestLayer | StackCommandStackRemoveLayer | StackCommandStackSetLayer | StackCommandStackSetModel | StackCommandStackSetSeed | StackCommandStackStroke:
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
                if not isinstance(data, dict):
                    raise TypeError()
                componentsschemas_stack_command_type_8 = StackCommandStackSetSeed.from_dict(data)



                return componentsschemas_stack_command_type_8

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
