from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.export_file_choice import ExportFileChoice
  from ..models.export_padding_info import ExportPaddingInfo





T = TypeVar("T", bound="ExportSetOptions")



@_attrs_define
class ExportSetOptions:
    """ One texture set's changes over the whole export's (#153): its own size, padding, dithering and files. Null fields
    keep what the export says for every set.

        Attributes:
            set_ (str): The texture set, by name.
            size (int | None | Unset): The longest side of this set's files.
            filter_ (None | str | Unset): How this set's files change size.
            padding (ExportPaddingInfo | None | Unset):
            dither (bool | None | Unset): Whether this set's 8-bit files are dithered.
            files (list[ExportFileChoice] | None | Unset): This set's files turned off or written in another format.
     """

    set_: str
    size: int | None | Unset = UNSET
    filter_: None | str | Unset = UNSET
    padding: ExportPaddingInfo | None | Unset = UNSET
    dither: bool | None | Unset = UNSET
    files: list[ExportFileChoice] | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_file_choice import ExportFileChoice # noqa: PLC0415
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        set_ = self.set_

        size: int | None | Unset
        if isinstance(self.size, Unset):
            size = UNSET
        else:
            size = self.size

        filter_: None | str | Unset
        if isinstance(self.filter_, Unset):
            filter_ = UNSET
        else:
            filter_ = self.filter_

        padding: dict[str, Any] | None | Unset
        if isinstance(self.padding, Unset):
            padding = UNSET
        elif isinstance(self.padding, ExportPaddingInfo):
            padding = self.padding.to_dict()
        else:
            padding = self.padding

        dither: bool | None | Unset
        if isinstance(self.dither, Unset):
            dither = UNSET
        else:
            dither = self.dither

        files: list[dict[str, Any]] | None | Unset
        if isinstance(self.files, Unset):
            files = UNSET
        elif isinstance(self.files, list):
            files = []
            for files_type_1_item_data in self.files:
                files_type_1_item = files_type_1_item_data.to_dict()
                files.append(files_type_1_item)


        else:
            files = self.files


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "set": set_,
        })
        if size is not UNSET:
            field_dict["size"] = size
        if filter_ is not UNSET:
            field_dict["filter"] = filter_
        if padding is not UNSET:
            field_dict["padding"] = padding
        if dither is not UNSET:
            field_dict["dither"] = dither
        if files is not UNSET:
            field_dict["files"] = files

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_file_choice import ExportFileChoice # noqa: PLC0415
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        d = dict(src_dict)
        set_ = d.pop("set")

        def _parse_size(data: object) -> int | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(int | None | Unset, data)

        size = _parse_size(d.pop("size", UNSET))


        def _parse_filter_(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        filter_ = _parse_filter_(d.pop("filter", UNSET))


        def _parse_padding(data: object) -> ExportPaddingInfo | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                padding_type_1 = ExportPaddingInfo.from_dict(data)



                return padding_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(ExportPaddingInfo | None | Unset, data)

        padding = _parse_padding(d.pop("padding", UNSET))


        def _parse_dither(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        dither = _parse_dither(d.pop("dither", UNSET))


        def _parse_files(data: object) -> list[ExportFileChoice] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                files_type_1 = []
                _files_type_1 = data
                for files_type_1_item_data in (_files_type_1):
                    files_type_1_item = ExportFileChoice.from_dict(files_type_1_item_data)



                    files_type_1.append(files_type_1_item)

                return files_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[ExportFileChoice] | None | Unset, data)

        files = _parse_files(d.pop("files", UNSET))


        export_set_options = cls(
            set_=set_,
            size=size,
            filter_=filter_,
            padding=padding,
            dither=dither,
            files=files,
        )


        export_set_options.additional_properties = d
        return export_set_options

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
