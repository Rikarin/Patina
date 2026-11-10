from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.graph_diagnostic import GraphDiagnostic
  from ..models.package_dependency import PackageDependency





T = TypeVar("T", bound="PackagePlan")



@_attrs_define
class PackagePlan:
    """ What publishing a graph as a package would do, for a dialog to show before it does.

        Attributes:
            name (str): The package's name.
            role (str): What it would be used as.
            path (str): Where it would be written, below the library.
            replaces (bool): True when a package of that name is there already, and publishing replaces it.
            dependencies (list[PackageDependency]): What it reads. Publishing is refused while any has a problem.
            diagnostics (list[GraphDiagnostic]): What compiling it said; an error refuses it too.
     """

    name: str
    role: str
    path: str
    replaces: bool
    dependencies: list[PackageDependency]
    diagnostics: list[GraphDiagnostic]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_diagnostic import GraphDiagnostic # noqa: PLC0415
        from ..models.package_dependency import PackageDependency # noqa: PLC0415
        name = self.name

        role = self.role

        path = self.path

        replaces = self.replaces

        dependencies = []
        for dependencies_item_data in self.dependencies:
            dependencies_item = dependencies_item_data.to_dict()
            dependencies.append(dependencies_item)



        diagnostics = []
        for diagnostics_item_data in self.diagnostics:
            diagnostics_item = diagnostics_item_data.to_dict()
            diagnostics.append(diagnostics_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "name": name,
            "role": role,
            "path": path,
            "replaces": replaces,
            "dependencies": dependencies,
            "diagnostics": diagnostics,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_diagnostic import GraphDiagnostic # noqa: PLC0415
        from ..models.package_dependency import PackageDependency # noqa: PLC0415
        d = dict(src_dict)
        name = d.pop("name")

        role = d.pop("role")

        path = d.pop("path")

        replaces = d.pop("replaces")

        dependencies = []
        _dependencies = d.pop("dependencies")
        for dependencies_item_data in (_dependencies):
            dependencies_item = PackageDependency.from_dict(dependencies_item_data)



            dependencies.append(dependencies_item)


        diagnostics = []
        _diagnostics = d.pop("diagnostics")
        for diagnostics_item_data in (_diagnostics):
            diagnostics_item = GraphDiagnostic.from_dict(diagnostics_item_data)



            diagnostics.append(diagnostics_item)


        package_plan = cls(
            name=name,
            role=role,
            path=path,
            replaces=replaces,
            dependencies=dependencies,
            diagnostics=diagnostics,
        )


        package_plan.additional_properties = d
        return package_plan

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
