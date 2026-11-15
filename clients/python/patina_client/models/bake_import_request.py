from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
from uuid import UUID

if TYPE_CHECKING:
  from ..models.bake_import_map import BakeImportMap





T = TypeVar("T", bound="BakeImportRequest")



@_attrs_define
class BakeImportRequest:
    """ Maps baked in another program, brought in as a texturing document's bake (#144): the body of
    `POST /api/v1/library/project/bake/import`.

        Attributes:
            project (str): The project, below `projects/`.
            document (UUID): The texturing document, by its id, whose texture set the maps are of.
            maps (list[BakeImportMap]): Each map and the PNG of it, on the Server's machine.
            space (str): Whether the normal is in `tangent` or `object` space, as a bake's options say. Default: 'tangent'.
     """

    project: str
    document: UUID
    maps: list[BakeImportMap]
    space: str = 'tangent'
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.bake_import_map import BakeImportMap # noqa: PLC0415
        project = self.project

        document = str(self.document)

        maps = []
        for maps_item_data in self.maps:
            maps_item = maps_item_data.to_dict()
            maps.append(maps_item)



        space = self.space


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "document": document,
            "maps": maps,
            "space": space,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.bake_import_map import BakeImportMap # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        document = UUID(d.pop("document"))




        maps = []
        _maps = d.pop("maps")
        for maps_item_data in (_maps):
            maps_item = BakeImportMap.from_dict(maps_item_data)



            maps.append(maps_item)


        space = d.pop("space")

        bake_import_request = cls(
            project=project,
            document=document,
            maps=maps,
            space=space,
        )


        bake_import_request.additional_properties = d
        return bake_import_request

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
