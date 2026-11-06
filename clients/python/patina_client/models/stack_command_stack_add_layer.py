from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..models.stack_command_stack_add_layer_op import StackCommandStackAddLayerOp
from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.stack_command_stack_add_layer_values_type_1 import StackCommandStackAddLayerValuesType1





T = TypeVar("T", bound="StackCommandStackAddLayer")



@_attrs_define
class StackCommandStackAddLayer:
    """ Adds a layer at the top of the stack.

        Attributes:
            op (StackCommandStackAddLayerOp):
            kind (str): fill, paint, filter or group.
            name (str): What it is called.
            values (None | StackCommandStackAddLayerValuesType1 | Unset): What a fill writes to each channel, as four
                numbers. Ignored for other kinds.
            parent (None | str | Unset): The group it goes at the top of, or null for the stack itself.
            filter_ (None | str | Unset): What a filter layer does: Levels, Hsl, Blur, Invert or Grayscale.
     """

    op: StackCommandStackAddLayerOp
    kind: str
    name: str
    values: None | StackCommandStackAddLayerValuesType1 | Unset = UNSET
    parent: None | str | Unset = UNSET
    filter_: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.stack_command_stack_add_layer_values_type_1 import StackCommandStackAddLayerValuesType1 # noqa: PLC0415
        op = self.op.value

        kind = self.kind

        name = self.name

        values: dict[str, Any] | None | Unset
        if isinstance(self.values, Unset):
            values = UNSET
        elif isinstance(self.values, StackCommandStackAddLayerValuesType1):
            values = self.values.to_dict()
        else:
            values = self.values

        parent: None | str | Unset
        if isinstance(self.parent, Unset):
            parent = UNSET
        else:
            parent = self.parent

        filter_: None | str | Unset
        if isinstance(self.filter_, Unset):
            filter_ = UNSET
        else:
            filter_ = self.filter_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "op": op,
            "kind": kind,
            "name": name,
        })
        if values is not UNSET:
            field_dict["values"] = values
        if parent is not UNSET:
            field_dict["parent"] = parent
        if filter_ is not UNSET:
            field_dict["filter"] = filter_

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.stack_command_stack_add_layer_values_type_1 import StackCommandStackAddLayerValuesType1 # noqa: PLC0415
        d = dict(src_dict)
        op = StackCommandStackAddLayerOp(d.pop("op"))




        kind = d.pop("kind")

        name = d.pop("name")

        def _parse_values(data: object) -> None | StackCommandStackAddLayerValuesType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                values_type_1 = StackCommandStackAddLayerValuesType1.from_dict(data)



                return values_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | StackCommandStackAddLayerValuesType1 | Unset, data)

        values = _parse_values(d.pop("values", UNSET))


        def _parse_parent(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        parent = _parse_parent(d.pop("parent", UNSET))


        def _parse_filter_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        filter_ = _parse_filter_(d.pop("filter", UNSET))


        stack_command_stack_add_layer = cls(
            op=op,
            kind=kind,
            name=name,
            values=values,
            parent=parent,
            filter_=filter_,
        )


        stack_command_stack_add_layer.additional_properties = d
        return stack_command_stack_add_layer

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
