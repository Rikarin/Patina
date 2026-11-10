from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.package_compound import PackageCompound
  from ..models.package_file import PackageFile





T = TypeVar("T", bound="PackageManifest")



@_attrs_define
class PackageManifest:
    """ A package's `package.yaml` (ADR 0027): what a `.ptpkg` folder holds besides its graph, and what made
    it. The graph's own file holds its parameters, presets and what it says about itself, so this repeats none of
    them.

        Attributes:
            format_ (int): The shape of the package. A reader refuses one newer than it knows.
            name (str): What the package is called, which is its folder's and its graph's name.
            graph (str): The graph's file, beside this one.
            role (str): What it is used as: `material`, `mask`, `filter` or `brush`.
            writer (str): The Patina that published it, as `Patina 0.9.0`.
            compounds (list[PackageCompound]): The compounds it carries under `Compounds/`, and what each was called where
                it came from.
            shipped (list[str]): The compounds it reads from those that ship with Patina, which it does not carry.
            files (list[PackageFile]): The pictures it carries under `files/`, and how the graph named each.
     """

    format_: int
    name: str
    graph: str
    role: str
    writer: str
    compounds: list[PackageCompound]
    shipped: list[str]
    files: list[PackageFile]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.package_compound import PackageCompound # noqa: PLC0415
        from ..models.package_file import PackageFile # noqa: PLC0415
        format_ = self.format_

        name = self.name

        graph = self.graph

        role = self.role

        writer = self.writer

        compounds = []
        for compounds_item_data in self.compounds:
            compounds_item = compounds_item_data.to_dict()
            compounds.append(compounds_item)



        shipped = self.shipped



        files = []
        for files_item_data in self.files:
            files_item = files_item_data.to_dict()
            files.append(files_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "format": format_,
            "name": name,
            "graph": graph,
            "role": role,
            "writer": writer,
            "compounds": compounds,
            "shipped": shipped,
            "files": files,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.package_compound import PackageCompound # noqa: PLC0415
        from ..models.package_file import PackageFile # noqa: PLC0415
        d = dict(src_dict)
        format_ = d.pop("format")

        name = d.pop("name")

        graph = d.pop("graph")

        role = d.pop("role")

        writer = d.pop("writer")

        compounds = []
        _compounds = d.pop("compounds")
        for compounds_item_data in (_compounds):
            compounds_item = PackageCompound.from_dict(compounds_item_data)



            compounds.append(compounds_item)


        shipped = cast(list[str], d.pop("shipped"))


        files = []
        _files = d.pop("files")
        for files_item_data in (_files):
            files_item = PackageFile.from_dict(files_item_data)



            files.append(files_item)


        package_manifest = cls(
            format_=format_,
            name=name,
            graph=graph,
            role=role,
            writer=writer,
            compounds=compounds,
            shipped=shipped,
            files=files,
        )


        package_manifest.additional_properties = d
        return package_manifest

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
