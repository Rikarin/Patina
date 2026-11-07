from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_setting_definition import GraphSettingDefinition





T = TypeVar("T", bound="MaterialInfo")



@_attrs_define
class MaterialInfo:
    """ Something a layer can be filled with (ADR 0012).

        Attributes:
            name (str): What it is called.
            kind (str): `graph`, which a fill reads, or `layered-material`, which unfolds into layers.
            source (str): What names it to the routes that use it.
            role (str): `material`, `mask`, `brush`, `filter`, or `empty` for a graph with no
                            output yet.
            origin (str): `shipped`, `project`, or `library` for another project.
            channels (list[str]): The maps a graph makes, by usage.
            parameters (list[GraphSettingDefinition] | None | Unset): The knobs a graph exposes, which a layer can set.
            project (None | str | Unset): The project it lives in, when that is another one.
     """

    name: str
    kind: str
    source: str
    role: str
    origin: str
    channels: list[str]
    parameters: list[GraphSettingDefinition] | None | Unset = UNSET
    project: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        name = self.name

        kind = self.kind

        source = self.source

        role = self.role

        origin = self.origin

        channels = self.channels



        parameters: list[dict[str, Any]] | None | Unset
        if isinstance(self.parameters, Unset):
            parameters = UNSET
        elif isinstance(self.parameters, list):
            parameters = []
            for parameters_type_1_item_data in self.parameters:
                parameters_type_1_item = parameters_type_1_item_data.to_dict()
                parameters.append(parameters_type_1_item)


        else:
            parameters = self.parameters

        project: None | str | Unset
        if isinstance(self.project, Unset):
            project = UNSET
        else:
            project = self.project


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "kind": kind,
            "source": source,
            "role": role,
            "origin": origin,
            "channels": channels,
        })
        if parameters is not UNSET:
            field_dict["parameters"] = parameters
        if project is not UNSET:
            field_dict["project"] = project

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        d = dict(src_dict)
        name = d.pop("name")

        kind = d.pop("kind")

        source = d.pop("source")

        role = d.pop("role")

        origin = d.pop("origin")

        channels = cast(list[str], d.pop("channels"))


        def _parse_parameters(data: object) -> list[GraphSettingDefinition] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                parameters_type_1 = []
                _parameters_type_1 = data
                for parameters_type_1_item_data in (_parameters_type_1):
                    parameters_type_1_item = GraphSettingDefinition.from_dict(parameters_type_1_item_data)



                    parameters_type_1.append(parameters_type_1_item)

                return parameters_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[GraphSettingDefinition] | None | Unset, data)

        parameters = _parse_parameters(d.pop("parameters", UNSET))


        def _parse_project(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        project = _parse_project(d.pop("project", UNSET))


        material_info = cls(
            name=name,
            kind=kind,
            source=source,
            role=role,
            origin=origin,
            channels=channels,
            parameters=parameters,
            project=project,
        )


        material_info.additional_properties = d
        return material_info

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
