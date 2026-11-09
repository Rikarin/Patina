from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_paste_layers_op import StackCommandStackPasteLayersOp
from ..types import UNSET, Unset
from typing import cast






T = TypeVar("T", bound="StackCommandStackPasteLayers")



@_attrs_define
class StackCommandStackPasteLayers:
    """ Pastes what a copy holds, as new layers with canvases of their own, in this stack or another. Limits to named
    meshes stay only in the stack they were made for.

        Attributes:
            op (StackCommandStackPasteLayersOp):
            clip (str): What `POST /stacks/{id}/clip` answered.
            as_instances (bool): Whether each comes in as an instance of the layer that was copied rather than as a copy, so
                that an edit to the
                original shows in it. The original has to be in this stack or another texture set of its model. Default: False.
            above (None | str | Unset): The layer they go just above, or null for the top of the stack.
     """

    op: StackCommandStackPasteLayersOp
    clip: str
    as_instances: bool = False
    above: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        clip = self.clip

        as_instances = self.as_instances

        above: None | str | Unset
        if isinstance(self.above, Unset):
            above = UNSET
        else:
            above = self.above


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "clip": clip,
            "asInstances": as_instances,
        })
        if above is not UNSET:
            field_dict["above"] = above

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackPasteLayersOp(d.pop("op"))




        clip = d.pop("clip")

        as_instances = d.pop("asInstances")

        def _parse_above(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        above = _parse_above(d.pop("above", UNSET))


        stack_command_stack_paste_layers = cls(
            op=op,
            clip=clip,
            as_instances=as_instances,
            above=above,
        )


        stack_command_stack_paste_layers.additional_properties = d
        return stack_command_stack_paste_layers

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
