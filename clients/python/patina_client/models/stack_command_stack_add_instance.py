from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_add_instance_op import StackCommandStackAddInstanceOp
from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_instance_ref import StackInstanceRef





T = TypeVar("T", bound="StackCommandStackAddInstance")



@_attrs_define
class StackCommandStackAddInstance:
    """ Adds an instance of a layer just above the chosen place: a layer that stands for the source, in this texture set
    or another of the model, so that an edit to the source shows in it.

        Attributes:
            op (StackCommandStackAddInstanceOp):
            source (StackInstanceRef): The layer an instance stands for.
            above (None | str | Unset): The layer it goes just above, or null for the top of the stack.
     """

    op: StackCommandStackAddInstanceOp
    source: StackInstanceRef
    above: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_instance_ref import StackInstanceRef # noqa: PLC0415
        op = self.op.value

        source = self.source.to_dict()

        above: None | str | Unset
        if isinstance(self.above, Unset):
            above = UNSET
        else:
            above = self.above


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "source": source,
        })
        if above is not UNSET:
            field_dict["above"] = above

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_instance_ref import StackInstanceRef # noqa: PLC0415
        d = dict(src_dict)
        op = StackCommandStackAddInstanceOp(d.pop("op"))




        source = StackInstanceRef.from_dict(d.pop("source"))




        def _parse_above(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        above = _parse_above(d.pop("above", UNSET))


        stack_command_stack_add_instance = cls(
            op=op,
            source=source,
            above=above,
        )


        stack_command_stack_add_instance.additional_properties = d
        return stack_command_stack_add_instance

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
