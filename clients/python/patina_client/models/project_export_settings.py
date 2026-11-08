from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.export_file_setting import ExportFileSetting
  from ..models.export_padding_info import ExportPaddingInfo





T = TypeVar("T", bound="ProjectExportSettings")



@_attrs_define
class ProjectExportSettings:
    """ A project's export settings, kept in its `project.yaml` so they travel with the library: what a client's
    export starts from next time. Any client reads and writes them; a build reads only what a request sends, so
    keeping them changes no key.

        Attributes:
            skipped (list[str]): The texture sets left out, by name; every other one is written.
            files (list[ExportFileSetting]): Files turned off or written in another format, per preset.
            preset (None | str | Unset): The preset's id.
            folder (None | str | Unset): Where files go, or null for the library's exports folder.
            size (int | None | Unset): The longest side of a file, or null for the maps' own size.
            filter_ (None | str | Unset): How a file changes size: box, bilinear or nearest.
            padding (ExportPaddingInfo | None | Unset):
            dither (bool | None | Unset): Whether 8-bit files are dithered.
     """

    skipped: list[str]
    files: list[ExportFileSetting]
    preset: None | str | Unset = UNSET
    folder: None | str | Unset = UNSET
    size: int | None | Unset = UNSET
    filter_: None | str | Unset = UNSET
    padding: ExportPaddingInfo | None | Unset = UNSET
    dither: bool | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_file_setting import ExportFileSetting # noqa: PLC0415
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        skipped = self.skipped



        files = []
        for files_item_data in self.files:
            files_item = files_item_data.to_dict()
            files.append(files_item)



        preset: None | str | Unset
        if isinstance(self.preset, Unset):
            preset = UNSET
        else:
            preset = self.preset

        folder: None | str | Unset
        if isinstance(self.folder, Unset):
            folder = UNSET
        else:
            folder = self.folder

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


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "skipped": skipped,
            "files": files,
        })
        if preset is not UNSET:
            field_dict["preset"] = preset
        if folder is not UNSET:
            field_dict["folder"] = folder
        if size is not UNSET:
            field_dict["size"] = size
        if filter_ is not UNSET:
            field_dict["filter"] = filter_
        if padding is not UNSET:
            field_dict["padding"] = padding
        if dither is not UNSET:
            field_dict["dither"] = dither

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_file_setting import ExportFileSetting # noqa: PLC0415
        from ..models.export_padding_info import ExportPaddingInfo # noqa: PLC0415
        d = dict(src_dict)
        skipped = cast(list[str], d.pop("skipped"))


        files = []
        _files = d.pop("files")
        for files_item_data in (_files):
            files_item = ExportFileSetting.from_dict(files_item_data)



            files.append(files_item)


        def _parse_preset(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        preset = _parse_preset(d.pop("preset", UNSET))


        def _parse_folder(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        folder = _parse_folder(d.pop("folder", UNSET))


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


        project_export_settings = cls(
            skipped=skipped,
            files=files,
            preset=preset,
            folder=folder,
            size=size,
            filter_=filter_,
            padding=padding,
            dither=dither,
        )


        project_export_settings.additional_properties = d
        return project_export_settings

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
