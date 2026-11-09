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
            part (str): With Into: Mask takes the copied layer's mask, entries, effects and painted canvases, and
                keeps the layer's own limit to some meshes; Content takes what the copied layer puts on top (its kind, its fill
                or canvas, its filter, a folder's layers) and keeps the layer's name, mask, blend, opacity and anchor point.
                Default: 'Mask'.
            above (None | str | Unset): The layer they go just above, or null for the top of the stack.
            into (None | str | Unset): A layer to paste part of the first copied layer into, rather than adding layers: its
                mask, or its content.
     """

    op: StackCommandStackPasteLayersOp
    clip: str
    as_instances: bool = False
    part: str = 'Mask'
    above: None | str | Unset = UNSET
    into: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        clip = self.clip

        as_instances = self.as_instances

        part = self.part

        above: None | str | Unset
        if isinstance(self.above, Unset):
            above = UNSET
        else:
            above = self.above

        into: None | str | Unset
        if isinstance(self.into, Unset):
            into = UNSET
        else:
            into = self.into


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "clip": clip,
            "asInstances": as_instances,
            "part": part,
        })
        if above is not UNSET:
            field_dict["above"] = above
        if into is not UNSET:
            field_dict["into"] = into

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackPasteLayersOp(d.pop("op"))




        clip = d.pop("clip")

        as_instances = d.pop("asInstances")

        part = d.pop("part")

        def _parse_above(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        above = _parse_above(d.pop("above", UNSET))


        def _parse_into(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        into = _parse_into(d.pop("into", UNSET))


        stack_command_stack_paste_layers = cls(
            op=op,
            clip=clip,
            as_instances=as_instances,
            part=part,
            above=above,
            into=into,
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
