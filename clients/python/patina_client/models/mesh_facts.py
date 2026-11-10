from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.mesh_material_facts import MeshMaterialFacts
  from ..models.mesh_part_facts import MeshPartFacts





T = TypeVar("T", bound="MeshFacts")



@_attrs_define
class MeshFacts:
    """ What a mesh file is, as the engine reads it (#146): the answer to `GET /library/mesh`.

        Attributes:
            file (str): The file's name.
            format_ (str): What the format is called, for a person.
            triangles (int): How many triangles, every part counted where it stands.
            vertices (int): How many vertices, after welding.
            parts (list[MeshPartFacts]): Each named mesh where the file places it.
            materials (list[MeshMaterialFacts]): Each material a triangle uses, which is each texture set the mesh has.
            uv_sets (int): The most texture coordinate sets any part has. Zero is a mesh with no layout.
            minimum (list[float]): The least corner of the box round the model, in the file's units.
            maximum (list[float]): The greatest corner of the box.
            vertex_colours (bool): Whether any part carries vertex colours.
            metres_per_unit (float | None | Unset): How long the file's unit is, in metres, when the file or its format
                says: glTF is in metres, and an FBX states
                its unit. Null for a file that does not say, as OBJ, PLY and STL never do.
     """

    file: str
    format_: str
    triangles: int
    vertices: int
    parts: list[MeshPartFacts]
    materials: list[MeshMaterialFacts]
    uv_sets: int
    minimum: list[float]
    maximum: list[float]
    vertex_colours: bool
    metres_per_unit: float | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.mesh_material_facts import MeshMaterialFacts # noqa: PLC0415
        from ..models.mesh_part_facts import MeshPartFacts # noqa: PLC0415
        file = self.file

        format_ = self.format_

        triangles = self.triangles

        vertices = self.vertices

        parts = []
        for parts_item_data in self.parts:
            parts_item = parts_item_data.to_dict()
            parts.append(parts_item)



        materials = []
        for materials_item_data in self.materials:
            materials_item = materials_item_data.to_dict()
            materials.append(materials_item)



        uv_sets = self.uv_sets

        minimum = self.minimum



        maximum = self.maximum



        vertex_colours = self.vertex_colours

        metres_per_unit: float | None | Unset
        if isinstance(self.metres_per_unit, Unset):
            metres_per_unit = UNSET
        else:
            metres_per_unit = self.metres_per_unit


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "file": file,
            "format": format_,
            "triangles": triangles,
            "vertices": vertices,
            "parts": parts,
            "materials": materials,
            "uvSets": uv_sets,
            "minimum": minimum,
            "maximum": maximum,
            "vertexColours": vertex_colours,
        })
        if metres_per_unit is not UNSET:
            field_dict["metresPerUnit"] = metres_per_unit

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.mesh_material_facts import MeshMaterialFacts # noqa: PLC0415
        from ..models.mesh_part_facts import MeshPartFacts # noqa: PLC0415
        d = dict(src_dict)
        file = d.pop("file")

        format_ = d.pop("format")

        triangles = d.pop("triangles")

        vertices = d.pop("vertices")

        parts = []
        _parts = d.pop("parts")
        for parts_item_data in (_parts):
            parts_item = MeshPartFacts.from_dict(parts_item_data)



            parts.append(parts_item)


        materials = []
        _materials = d.pop("materials")
        for materials_item_data in (_materials):
            materials_item = MeshMaterialFacts.from_dict(materials_item_data)



            materials.append(materials_item)


        uv_sets = d.pop("uvSets")

        minimum = cast(list[float], d.pop("minimum"))


        maximum = cast(list[float], d.pop("maximum"))


        vertex_colours = d.pop("vertexColours")

        def _parse_metres_per_unit(data: object) -> float | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(float | None | Unset, data)

        metres_per_unit = _parse_metres_per_unit(d.pop("metresPerUnit", UNSET))


        mesh_facts = cls(
            file=file,
            format_=format_,
            triangles=triangles,
            vertices=vertices,
            parts=parts,
            materials=materials,
            uv_sets=uv_sets,
            minimum=minimum,
            maximum=maximum,
            vertex_colours=vertex_colours,
            metres_per_unit=metres_per_unit,
        )


        mesh_facts.additional_properties = d
        return mesh_facts

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
