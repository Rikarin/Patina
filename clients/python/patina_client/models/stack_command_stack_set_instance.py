from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_instance_op import StackCommandStackSetInstanceOp
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_instance_ref import StackInstanceRef





T = TypeVar("T", bound="StackCommandStackSetInstance")



@_attrs_define
class StackCommandStackSetInstance:
    """ Points an instance at another layer, which it then stands for.

        Attributes:
            op (StackCommandStackSetInstanceOp):
            layer (str): The instance.
            source (StackInstanceRef): The layer an instance stands for.
     """

    op: StackCommandStackSetInstanceOp
    layer: str
    source: StackInstanceRef
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_instance_ref import StackInstanceRef # noqa: PLC0415
        op = self.op.value

        layer = self.layer

        source = self.source.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "layer": layer,
            "source": source,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_instance_ref import StackInstanceRef # noqa: PLC0415
        d = dict(src_dict)
        op = StackCommandStackSetInstanceOp(d.pop("op"))




        layer = d.pop("layer")

        source = StackInstanceRef.from_dict(d.pop("source"))




        stack_command_stack_set_instance = cls(
            op=op,
            layer=layer,
            source=source,
        )


        stack_command_stack_set_instance.additional_properties = d
        return stack_command_stack_set_instance

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
