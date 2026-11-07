from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_set_seed_op import StackCommandStackSetSeedOp






T = TypeVar("T", bound="StackCommandStackSetSeed")



@_attrs_define
class StackCommandStackSetSeed:
    """ Sets the number every procedural step of the stack derives from.

        Attributes:
            op (StackCommandStackSetSeedOp):
            seed (int): It.
     """

    op: StackCommandStackSetSeedOp
    seed: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        op = self.op.value

        seed = self.seed


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "seed": seed,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        op = StackCommandStackSetSeedOp(d.pop("op"))




        seed = d.pop("seed")

        stack_command_stack_set_seed = cls(
            op=op,
            seed=seed,
        )


        stack_command_stack_set_seed.additional_properties = d
        return stack_command_stack_set_seed

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
