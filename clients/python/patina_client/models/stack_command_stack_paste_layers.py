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
            above (None | str | Unset): The layer they go just above, or null for the top of the stack.
     """

    op: StackCommandStackPasteLayersOp
    clip: str
    above: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        clip = self.clip

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
        })
        if above is not UNSET:
            field_dict["above"] = above

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackPasteLayersOp(d.pop("op"))




        clip = d.pop("clip")

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
