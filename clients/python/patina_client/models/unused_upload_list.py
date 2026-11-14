from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.unused_file import UnusedFile





T = TypeVar("T", bound="UnusedUploadList")



@_attrs_define
class UnusedUploadList:
    """ What `GET /api/v1/library/uploads/unused` answers (#297).

        Attributes:
            files (list[UnusedFile]): The files sent from another computer that nothing names, in order of their path.
            bytes_ (int): The room they take.
     """

    files: list[UnusedFile]
    bytes_: int
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.unused_file import UnusedFile # noqa: PLC0415
        files = []
        for files_item_data in self.files:
            files_item = files_item_data.to_dict()
            files.append(files_item)



        bytes_ = self.bytes_


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "files": files,
            "bytes": bytes_,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.unused_file import UnusedFile # noqa: PLC0415
        d = dict(src_dict)
        files = []
        _files = d.pop("files")
        for files_item_data in (_files):
            files_item = UnusedFile.from_dict(files_item_data)



            files.append(files_item)


        bytes_ = d.pop("bytes")

        unused_upload_list = cls(
            files=files,
            bytes_=bytes_,
        )


        unused_upload_list.additional_properties = d
        return unused_upload_list

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
