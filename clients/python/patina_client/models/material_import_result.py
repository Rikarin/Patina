from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast






T = TypeVar("T", bound="MaterialImportResult")



@_attrs_define
class MaterialImportResult:
    """ What importing a material file made.

        Attributes:
            documents (list[str]): The graph made for each material, in the file's order: its path below the library, which
                the graph routes take.
            warnings (list[str]): What a person should know: an input left out, and why.
     """

    documents: list[str]
    warnings: list[str]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        documents = self.documents



        warnings = self.warnings




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "documents": documents,
            "warnings": warnings,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        d = dict(src_dict)
        documents = cast(list[str], d.pop("documents"))


        warnings = cast(list[str], d.pop("warnings"))


        material_import_result = cls(
            documents=documents,
            warnings=warnings,
        )


        material_import_result.additional_properties = d
        return material_import_result

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
