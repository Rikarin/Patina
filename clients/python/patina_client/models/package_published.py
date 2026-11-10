from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.package_manifest import PackageManifest





T = TypeVar("T", bound="PackagePublished")



@_attrs_define
class PackagePublished:
    """ A package that has been written.

        Attributes:
            path (str): Its folder, below the library.
            manifest (PackageManifest): A package's `package.yaml` (ADR 0027): what a `.ptpkg` folder holds besides its
                graph, and what made
                it. The graph's own file holds its parameters, presets and what it says about itself, so this repeats none of
                them.
     """

    path: str
    manifest: PackageManifest
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.package_manifest import PackageManifest # noqa: PLC0415
        path = self.path

        manifest = self.manifest.to_dict()


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "path": path,
            "manifest": manifest,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.package_manifest import PackageManifest # noqa: PLC0415
        d = dict(src_dict)
        path = d.pop("path")

        manifest = PackageManifest.from_dict(d.pop("manifest"))




        package_published = cls(
            path=path,
            manifest=manifest,
        )


        package_published.additional_properties = d
        return package_published

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
