from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast
from uuid import UUID

if TYPE_CHECKING:
  from ..models.export_report import ExportReport
  from ..models.output_report import OutputReport





T = TypeVar("T", bound="ProjectReport")



@_attrs_define
class ProjectReport:
    """ What a build did to one project. Only a project something happened to has one.

        Attributes:
            id (UUID): The project.
            name (str): Its name.
            path (str): Its path below `projects/`.
            outputs (list[OutputReport]): Its outputs that were touched.
            exports (list[ExportReport]): Its exports that were touched.
     """

    id: UUID
    name: str
    path: str
    outputs: list[OutputReport]
    exports: list[ExportReport]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_report import ExportReport # noqa: PLC0415
        from ..models.output_report import OutputReport # noqa: PLC0415
        id = str(self.id)

        name = self.name

        path = self.path

        outputs = []
        for outputs_item_data in self.outputs:
            outputs_item = outputs_item_data.to_dict()
            outputs.append(outputs_item)



        exports = []
        for exports_item_data in self.exports:
            exports_item = exports_item_data.to_dict()
            exports.append(exports_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "name": name,
            "path": path,
            "outputs": outputs,
            "exports": exports,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_report import ExportReport # noqa: PLC0415
        from ..models.output_report import OutputReport # noqa: PLC0415
        d = dict(src_dict)
        id = UUID(d.pop("id"))




        name = d.pop("name")

        path = d.pop("path")

        outputs = []
        _outputs = d.pop("outputs")
        for outputs_item_data in (_outputs):
            outputs_item = OutputReport.from_dict(outputs_item_data)



            outputs.append(outputs_item)


        exports = []
        _exports = d.pop("exports")
        for exports_item_data in (_exports):
            exports_item = ExportReport.from_dict(exports_item_data)



            exports.append(exports_item)


        project_report = cls(
            id=id,
            name=name,
            path=path,
            outputs=outputs,
            exports=exports,
        )


        project_report.additional_properties = d
        return project_report

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
