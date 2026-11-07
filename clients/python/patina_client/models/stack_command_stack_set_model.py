from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_model_op import StackCommandStackSetModelOp






T = TypeVar("T", bound="StackCommandStackSetModel")



@_attrs_define
class StackCommandStackSetModel:
    """ Names the mesh a stack is painted on, which is undone like any other edit.

        Attributes:
            op (StackCommandStackSetModelOp):
            model (str): A model file, absolute or relative to the stack. Empty takes the mesh away.
     """

    op: StackCommandStackSetModelOp
    model: str
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        model = self.model


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "model": model,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackSetModelOp(d.pop("op"))




        model = d.pop("model")

        stack_command_stack_set_model = cls(
            op=op,
            model=model,
        )


        stack_command_stack_set_model.additional_properties = d
        return stack_command_stack_set_model

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
