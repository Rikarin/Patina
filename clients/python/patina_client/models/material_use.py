from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.material_use_parameters_type_1 import MaterialUseParametersType1





T = TypeVar("T", bound="MaterialUse")



@_attrs_define
class MaterialUse:
    """ A graph ready for a layer.

        Attributes:
            graph (str): What a fill layer's `graph` takes.
            parameters (MaterialUseParametersType1 | None | Unset): What a material preset sets the graph's parameters to,
                which a fill layer's `parameters` take, or null
                for a material used as it is (#124).
     """

    graph: str
    parameters: MaterialUseParametersType1 | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.material_use_parameters_type_1 import MaterialUseParametersType1 # noqa: PLC0415
        graph = self.graph

        parameters: dict[str, Any] | None | Unset
        if isinstance(self.parameters, Unset):
            parameters = UNSET
        elif isinstance(self.parameters, MaterialUseParametersType1):
            parameters = self.parameters.to_dict()
        else:
            parameters = self.parameters


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "graph": graph,
        })
        if parameters is not UNSET:
            field_dict["parameters"] = parameters

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.material_use_parameters_type_1 import MaterialUseParametersType1 # noqa: PLC0415
        d = dict(src_dict)
        graph = d.pop("graph")

        def _parse_parameters(data: object) -> MaterialUseParametersType1 | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                parameters_type_1 = MaterialUseParametersType1.from_dict(data)



                return parameters_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(MaterialUseParametersType1 | None | Unset, data)

        parameters = _parse_parameters(d.pop("parameters", UNSET))


        material_use = cls(
            graph=graph,
            parameters=parameters,
        )


        material_use.additional_properties = d
        return material_use

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
