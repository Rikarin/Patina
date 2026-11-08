from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_command_graph_add_comment import GraphCommandGraphAddComment
  from ..models.graph_command_graph_add_group import GraphCommandGraphAddGroup
  from ..models.graph_command_graph_add_node import GraphCommandGraphAddNode
  from ..models.graph_command_graph_add_parameter import GraphCommandGraphAddParameter
  from ..models.graph_command_graph_add_port import GraphCommandGraphAddPort
  from ..models.graph_command_graph_apply_preset import GraphCommandGraphApplyPreset
  from ..models.graph_command_graph_arrange import GraphCommandGraphArrange
  from ..models.graph_command_graph_connect import GraphCommandGraphConnect
  from ..models.graph_command_graph_disconnect import GraphCommandGraphDisconnect
  from ..models.graph_command_graph_move_nodes import GraphCommandGraphMoveNodes
  from ..models.graph_command_graph_remove_comment import GraphCommandGraphRemoveComment
  from ..models.graph_command_graph_remove_group import GraphCommandGraphRemoveGroup
  from ..models.graph_command_graph_remove_nodes import GraphCommandGraphRemoveNodes
  from ..models.graph_command_graph_remove_parameter import GraphCommandGraphRemoveParameter
  from ..models.graph_command_graph_remove_port import GraphCommandGraphRemovePort
  from ..models.graph_command_graph_remove_preset import GraphCommandGraphRemovePreset
  from ..models.graph_command_graph_save_preset import GraphCommandGraphSavePreset
  from ..models.graph_command_graph_set_bypass import GraphCommandGraphSetBypass
  from ..models.graph_command_graph_set_comment import GraphCommandGraphSetComment
  from ..models.graph_command_graph_set_group import GraphCommandGraphSetGroup
  from ..models.graph_command_graph_set_parameter import GraphCommandGraphSetParameter
  from ..models.graph_command_graph_set_port import GraphCommandGraphSetPort
  from ..models.graph_command_graph_set_setting import GraphCommandGraphSetSetting
  from ..models.graph_command_graph_set_text import GraphCommandGraphSetText
  from ..models.graph_command_graph_set_value import GraphCommandGraphSetValue
  from ..models.graph_command_graph_wire import GraphCommandGraphWire





T = TypeVar("T", bound="GraphCommandBatch")



@_attrs_define
class GraphCommandBatch:
    """ Some edits, applied whole or not at all, and undone as one step.

        Attributes:
            commands (list[GraphCommandGraphAddComment | GraphCommandGraphAddGroup | GraphCommandGraphAddNode |
                GraphCommandGraphAddParameter | GraphCommandGraphAddPort | GraphCommandGraphApplyPreset |
                GraphCommandGraphArrange | GraphCommandGraphConnect | GraphCommandGraphDisconnect | GraphCommandGraphMoveNodes |
                GraphCommandGraphRemoveComment | GraphCommandGraphRemoveGroup | GraphCommandGraphRemoveNodes |
                GraphCommandGraphRemoveParameter | GraphCommandGraphRemovePort | GraphCommandGraphRemovePreset |
                GraphCommandGraphSavePreset | GraphCommandGraphSetBypass | GraphCommandGraphSetComment |
                GraphCommandGraphSetGroup | GraphCommandGraphSetParameter | GraphCommandGraphSetPort |
                GraphCommandGraphSetSetting | GraphCommandGraphSetText | GraphCommandGraphSetValue | GraphCommandGraphWire]):
                What to do, in order.
            merge (bool): True to fold this batch into the last one when both change the same things, so that a whole drag
                is one undo.
                A client leaves it false on the first batch of a drag. Default: False.
            label (None | str | Unset): What the undo history calls it, or null for a name from the commands.
     """

    commands: list[GraphCommandGraphAddComment | GraphCommandGraphAddGroup | GraphCommandGraphAddNode | GraphCommandGraphAddParameter | GraphCommandGraphAddPort | GraphCommandGraphApplyPreset | GraphCommandGraphArrange | GraphCommandGraphConnect | GraphCommandGraphDisconnect | GraphCommandGraphMoveNodes | GraphCommandGraphRemoveComment | GraphCommandGraphRemoveGroup | GraphCommandGraphRemoveNodes | GraphCommandGraphRemoveParameter | GraphCommandGraphRemovePort | GraphCommandGraphRemovePreset | GraphCommandGraphSavePreset | GraphCommandGraphSetBypass | GraphCommandGraphSetComment | GraphCommandGraphSetGroup | GraphCommandGraphSetParameter | GraphCommandGraphSetPort | GraphCommandGraphSetSetting | GraphCommandGraphSetText | GraphCommandGraphSetValue | GraphCommandGraphWire]
    merge: bool = False
    label: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_command_graph_add_comment import GraphCommandGraphAddComment # noqa: PLC0415
        from ..models.graph_command_graph_add_group import GraphCommandGraphAddGroup # noqa: PLC0415
        from ..models.graph_command_graph_add_node import GraphCommandGraphAddNode # noqa: PLC0415
        from ..models.graph_command_graph_add_parameter import GraphCommandGraphAddParameter # noqa: PLC0415
        from ..models.graph_command_graph_add_port import GraphCommandGraphAddPort # noqa: PLC0415
        from ..models.graph_command_graph_apply_preset import GraphCommandGraphApplyPreset # noqa: PLC0415
        from ..models.graph_command_graph_arrange import GraphCommandGraphArrange # noqa: PLC0415
        from ..models.graph_command_graph_connect import GraphCommandGraphConnect # noqa: PLC0415
        from ..models.graph_command_graph_disconnect import GraphCommandGraphDisconnect # noqa: PLC0415
        from ..models.graph_command_graph_move_nodes import GraphCommandGraphMoveNodes # noqa: PLC0415
        from ..models.graph_command_graph_remove_comment import GraphCommandGraphRemoveComment # noqa: PLC0415
        from ..models.graph_command_graph_remove_group import GraphCommandGraphRemoveGroup # noqa: PLC0415
        from ..models.graph_command_graph_remove_nodes import GraphCommandGraphRemoveNodes # noqa: PLC0415
        from ..models.graph_command_graph_remove_parameter import GraphCommandGraphRemoveParameter # noqa: PLC0415
        from ..models.graph_command_graph_remove_port import GraphCommandGraphRemovePort # noqa: PLC0415
        from ..models.graph_command_graph_remove_preset import GraphCommandGraphRemovePreset # noqa: PLC0415
        from ..models.graph_command_graph_save_preset import GraphCommandGraphSavePreset # noqa: PLC0415
        from ..models.graph_command_graph_set_bypass import GraphCommandGraphSetBypass # noqa: PLC0415
        from ..models.graph_command_graph_set_comment import GraphCommandGraphSetComment # noqa: PLC0415
        from ..models.graph_command_graph_set_group import GraphCommandGraphSetGroup # noqa: PLC0415
        from ..models.graph_command_graph_set_parameter import GraphCommandGraphSetParameter # noqa: PLC0415
        from ..models.graph_command_graph_set_port import GraphCommandGraphSetPort # noqa: PLC0415
        from ..models.graph_command_graph_set_setting import GraphCommandGraphSetSetting # noqa: PLC0415
        from ..models.graph_command_graph_set_text import GraphCommandGraphSetText # noqa: PLC0415
        from ..models.graph_command_graph_set_value import GraphCommandGraphSetValue # noqa: PLC0415
        from ..models.graph_command_graph_wire import GraphCommandGraphWire # noqa: PLC0415
        commands = []
        for commands_item_data in self.commands:
            commands_item: dict[str, Any]
            if isinstance(commands_item_data, GraphCommandGraphAddNode):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphRemoveNodes):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphMoveNodes):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphConnect):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphWire):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphDisconnect):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphSetValue):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphSetText):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphSetSetting):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphAddParameter):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphSetParameter):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphRemoveParameter):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphAddPort):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphSetPort):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphRemovePort):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphAddGroup):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphSetGroup):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphRemoveGroup):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphAddComment):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphSetComment):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphRemoveComment):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphSetBypass):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphArrange):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphSavePreset):
                commands_item = commands_item_data.to_dict()
            elif isinstance(commands_item_data, GraphCommandGraphRemovePreset):
                commands_item = commands_item_data.to_dict()
            else:
                commands_item = commands_item_data.to_dict()

            commands.append(commands_item)



        merge = self.merge

        label: None | str | Unset
        if isinstance(self.label, Unset):
            label = UNSET
        else:
            label = self.label


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "commands": commands,
            "merge": merge,
        })
        if label is not UNSET:
            field_dict["label"] = label

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_command_graph_add_comment import GraphCommandGraphAddComment # noqa: PLC0415
        from ..models.graph_command_graph_add_group import GraphCommandGraphAddGroup # noqa: PLC0415
        from ..models.graph_command_graph_add_node import GraphCommandGraphAddNode # noqa: PLC0415
        from ..models.graph_command_graph_add_parameter import GraphCommandGraphAddParameter # noqa: PLC0415
        from ..models.graph_command_graph_add_port import GraphCommandGraphAddPort # noqa: PLC0415
        from ..models.graph_command_graph_apply_preset import GraphCommandGraphApplyPreset # noqa: PLC0415
        from ..models.graph_command_graph_arrange import GraphCommandGraphArrange # noqa: PLC0415
        from ..models.graph_command_graph_connect import GraphCommandGraphConnect # noqa: PLC0415
        from ..models.graph_command_graph_disconnect import GraphCommandGraphDisconnect # noqa: PLC0415
        from ..models.graph_command_graph_move_nodes import GraphCommandGraphMoveNodes # noqa: PLC0415
        from ..models.graph_command_graph_remove_comment import GraphCommandGraphRemoveComment # noqa: PLC0415
        from ..models.graph_command_graph_remove_group import GraphCommandGraphRemoveGroup # noqa: PLC0415
        from ..models.graph_command_graph_remove_nodes import GraphCommandGraphRemoveNodes # noqa: PLC0415
        from ..models.graph_command_graph_remove_parameter import GraphCommandGraphRemoveParameter # noqa: PLC0415
        from ..models.graph_command_graph_remove_port import GraphCommandGraphRemovePort # noqa: PLC0415
        from ..models.graph_command_graph_remove_preset import GraphCommandGraphRemovePreset # noqa: PLC0415
        from ..models.graph_command_graph_save_preset import GraphCommandGraphSavePreset # noqa: PLC0415
        from ..models.graph_command_graph_set_bypass import GraphCommandGraphSetBypass # noqa: PLC0415
        from ..models.graph_command_graph_set_comment import GraphCommandGraphSetComment # noqa: PLC0415
        from ..models.graph_command_graph_set_group import GraphCommandGraphSetGroup # noqa: PLC0415
        from ..models.graph_command_graph_set_parameter import GraphCommandGraphSetParameter # noqa: PLC0415
        from ..models.graph_command_graph_set_port import GraphCommandGraphSetPort # noqa: PLC0415
        from ..models.graph_command_graph_set_setting import GraphCommandGraphSetSetting # noqa: PLC0415
        from ..models.graph_command_graph_set_text import GraphCommandGraphSetText # noqa: PLC0415
        from ..models.graph_command_graph_set_value import GraphCommandGraphSetValue # noqa: PLC0415
        from ..models.graph_command_graph_wire import GraphCommandGraphWire # noqa: PLC0415
        d = dict(src_dict)
        commands = []
        _commands = d.pop("commands")
        for commands_item_data in (_commands):
            def _parse_commands_item(data: object) -> GraphCommandGraphAddComment | GraphCommandGraphAddGroup | GraphCommandGraphAddNode | GraphCommandGraphAddParameter | GraphCommandGraphAddPort | GraphCommandGraphApplyPreset | GraphCommandGraphArrange | GraphCommandGraphConnect | GraphCommandGraphDisconnect | GraphCommandGraphMoveNodes | GraphCommandGraphRemoveComment | GraphCommandGraphRemoveGroup | GraphCommandGraphRemoveNodes | GraphCommandGraphRemoveParameter | GraphCommandGraphRemovePort | GraphCommandGraphRemovePreset | GraphCommandGraphSavePreset | GraphCommandGraphSetBypass | GraphCommandGraphSetComment | GraphCommandGraphSetGroup | GraphCommandGraphSetParameter | GraphCommandGraphSetPort | GraphCommandGraphSetSetting | GraphCommandGraphSetText | GraphCommandGraphSetValue | GraphCommandGraphWire:
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_0 = GraphCommandGraphAddNode.from_dict(data)



                    return componentsschemas_graph_command_type_0
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_1 = GraphCommandGraphRemoveNodes.from_dict(data)



                    return componentsschemas_graph_command_type_1
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_2 = GraphCommandGraphMoveNodes.from_dict(data)



                    return componentsschemas_graph_command_type_2
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_3 = GraphCommandGraphConnect.from_dict(data)



                    return componentsschemas_graph_command_type_3
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_4 = GraphCommandGraphWire.from_dict(data)



                    return componentsschemas_graph_command_type_4
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_5 = GraphCommandGraphDisconnect.from_dict(data)



                    return componentsschemas_graph_command_type_5
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_6 = GraphCommandGraphSetValue.from_dict(data)



                    return componentsschemas_graph_command_type_6
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_7 = GraphCommandGraphSetText.from_dict(data)



                    return componentsschemas_graph_command_type_7
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_8 = GraphCommandGraphSetSetting.from_dict(data)



                    return componentsschemas_graph_command_type_8
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_9 = GraphCommandGraphAddParameter.from_dict(data)



                    return componentsschemas_graph_command_type_9
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_10 = GraphCommandGraphSetParameter.from_dict(data)



                    return componentsschemas_graph_command_type_10
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_11 = GraphCommandGraphRemoveParameter.from_dict(data)



                    return componentsschemas_graph_command_type_11
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_12 = GraphCommandGraphAddPort.from_dict(data)



                    return componentsschemas_graph_command_type_12
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_13 = GraphCommandGraphSetPort.from_dict(data)



                    return componentsschemas_graph_command_type_13
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_14 = GraphCommandGraphRemovePort.from_dict(data)



                    return componentsschemas_graph_command_type_14
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_15 = GraphCommandGraphAddGroup.from_dict(data)



                    return componentsschemas_graph_command_type_15
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_16 = GraphCommandGraphSetGroup.from_dict(data)



                    return componentsschemas_graph_command_type_16
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_17 = GraphCommandGraphRemoveGroup.from_dict(data)



                    return componentsschemas_graph_command_type_17
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_18 = GraphCommandGraphAddComment.from_dict(data)



                    return componentsschemas_graph_command_type_18
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_19 = GraphCommandGraphSetComment.from_dict(data)



                    return componentsschemas_graph_command_type_19
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_20 = GraphCommandGraphRemoveComment.from_dict(data)



                    return componentsschemas_graph_command_type_20
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_21 = GraphCommandGraphSetBypass.from_dict(data)



                    return componentsschemas_graph_command_type_21
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_22 = GraphCommandGraphArrange.from_dict(data)



                    return componentsschemas_graph_command_type_22
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_23 = GraphCommandGraphSavePreset.from_dict(data)



                    return componentsschemas_graph_command_type_23
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                try:
                    if not isinstance(data, dict):
                        raise TypeError()
                    componentsschemas_graph_command_type_24 = GraphCommandGraphRemovePreset.from_dict(data)



                    return componentsschemas_graph_command_type_24
                except (TypeError, ValueError, AttributeError, KeyError):
                    pass
                if not isinstance(data, dict):
                    raise TypeError()
                componentsschemas_graph_command_type_25 = GraphCommandGraphApplyPreset.from_dict(data)



                return componentsschemas_graph_command_type_25

            commands_item = _parse_commands_item(commands_item_data)

            commands.append(commands_item)


        merge = d.pop("merge")

        def _parse_label(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        label = _parse_label(d.pop("label", UNSET))


        graph_command_batch = cls(
            commands=commands,
            merge=merge,
            label=label,
        )


        graph_command_batch.additional_properties = d
        return graph_command_batch

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
