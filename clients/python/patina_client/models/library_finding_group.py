from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.library_finding import LibraryFinding





T = TypeVar("T", bound="LibraryFindingGroup")



@_attrs_define
class LibraryFindingGroup:
    """ What one kind of finding of a library check came to, as `patina library check` prints it.

        Attributes:
            kind (str): What was found, such as `MissingReference` or `StaleOutput`.
            is_error (bool): Whether the library is wrong, as opposed to having work to do or room to tidy.
            count (int): How many there are in all.
            findings (list[LibraryFinding]): The first of them, at most int LibraryHealth.FindingsPerKind.
     """

    kind: str
    is_error: bool
    count: int
    findings: list[LibraryFinding]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.library_finding import LibraryFinding # noqa: PLC0415
        kind = self.kind

        is_error = self.is_error

        count = self.count

        findings = []
        for findings_item_data in self.findings:
            findings_item = findings_item_data.to_dict()
            findings.append(findings_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "kind": kind,
            "isError": is_error,
            "count": count,
            "findings": findings,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.library_finding import LibraryFinding # noqa: PLC0415
        d = dict(src_dict)
        kind = d.pop("kind")

        is_error = d.pop("isError")

        count = d.pop("count")

        findings = []
        _findings = d.pop("findings")
        for findings_item_data in (_findings):
            findings_item = LibraryFinding.from_dict(findings_item_data)



            findings.append(findings_item)


        library_finding_group = cls(
            kind=kind,
            is_error=is_error,
            count=count,
            findings=findings,
        )


        library_finding_group.additional_properties = d
        return library_finding_group

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
