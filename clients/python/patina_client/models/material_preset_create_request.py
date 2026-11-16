from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.material_preset_create_request_parameters import MaterialPresetCreateRequestParameters





T = TypeVar("T", bound="MaterialPresetCreateRequest")



@_attrs_define
class MaterialPresetCreateRequest:
    """ The body of `POST /api/v1/library/materials/presets`, which keeps a graph fill's material and parameters
    as a new material preset of the project.

        Attributes:
            project (str): The project, below `projects/`.
            name (str): What to call it, which names its file.
            graph (str): The material, as a graph fill in the project reads it (its `graph`), which the Server takes back to
                where
                the shelf has it.
            parameters (MaterialPresetCreateRequestParameters): What the fill sets the material's parameters to (its
                `parameters`).
     """

    project: str
    name: str
    graph: str
    parameters: MaterialPresetCreateRequestParameters
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.material_preset_create_request_parameters import MaterialPresetCreateRequestParameters # noqa: PLC0415
        project = self.project

        name = self.name

        graph = self.graph

        parameters = self.parameters.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "name": name,
            "graph": graph,
            "parameters": parameters,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.material_preset_create_request_parameters import MaterialPresetCreateRequestParameters # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        name = d.pop("name")

        graph = d.pop("graph")

        parameters = MaterialPresetCreateRequestParameters.from_dict(d.pop("parameters"))




        material_preset_create_request = cls(
            project=project,
            name=name,
            graph=graph,
            parameters=parameters,
        )


        material_preset_create_request.additional_properties = d
        return material_preset_create_request

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
