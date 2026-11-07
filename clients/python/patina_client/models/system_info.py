from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.file_formats import FileFormats
  from ..models.host_info import HostInfo
  from ..models.worker_status import WorkerStatus





T = TypeVar("T", bound="SystemInfo")



@_attrs_define
class SystemInfo:
    """ The reply to `GET /api/v1/system`.

        Attributes:
            version (str): The Server's release version.
            api_version (int): The major version of the REST API; it is the `v1` in the URL.
            host (HostInfo): The machine and runtime a process runs on.
            worker (WorkerStatus): The Worker as the Server sees it. All but `Connected` are null while there is none.
            texture_sides (list[int]): The texture sides a client offers, smallest first.
            default_texture_side (int): The side a new document gets when nobody says.
            formats (FileFormats): What a client may offer, with the dot.
            library (None | str | Unset): The library this Server owns, or null when it was started without one.
     """

    version: str
    api_version: int
    host: HostInfo
    worker: WorkerStatus
    texture_sides: list[int]
    default_texture_side: int
    formats: FileFormats
    library: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.file_formats import FileFormats # noqa: PLC0415
        from ..models.host_info import HostInfo # noqa: PLC0415
        from ..models.worker_status import WorkerStatus # noqa: PLC0415
        version = self.version

        api_version = self.api_version

        host = self.host.to_dict()

        worker = self.worker.to_dict()

        texture_sides = self.texture_sides



        default_texture_side = self.default_texture_side

        formats = self.formats.to_dict()

        library: None | str | Unset
        if isinstance(self.library, Unset):
            library = UNSET
        else:
            library = self.library


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "version": version,
            "apiVersion": api_version,
            "host": host,
            "worker": worker,
            "textureSides": texture_sides,
            "defaultTextureSide": default_texture_side,
            "formats": formats,
        })
        if library is not UNSET:
            field_dict["library"] = library

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.file_formats import FileFormats # noqa: PLC0415
        from ..models.host_info import HostInfo # noqa: PLC0415
        from ..models.worker_status import WorkerStatus # noqa: PLC0415
        d = dict(src_dict)
        version = d.pop("version")

        api_version = d.pop("apiVersion")

        host = HostInfo.from_dict(d.pop("host"))




        worker = WorkerStatus.from_dict(d.pop("worker"))




        texture_sides = cast(list[int], d.pop("textureSides"))


        default_texture_side = d.pop("defaultTextureSide")

        formats = FileFormats.from_dict(d.pop("formats"))




        def _parse_library(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        library = _parse_library(d.pop("library", UNSET))


        system_info = cls(
            version=version,
            api_version=api_version,
            host=host,
            worker=worker,
            texture_sides=texture_sides,
            default_texture_side=default_texture_side,
            formats=formats,
            library=library,
        )


        system_info.additional_properties = d
        return system_info

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
