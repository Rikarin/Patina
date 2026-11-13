from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast
from uuid import UUID

if TYPE_CHECKING:
  from ..models.export_options import ExportOptions





T = TypeVar("T", bound="BuildRequest")



@_attrs_define
class BuildRequest:
    """ What to build.

        Attributes:
            asset (None | Unset | UUID): Only what reads this shared asset, directly or not. Null for the whole library.
            projects (list[UUID] | None | Unset): Only these projects. Null or empty for all of them.
            documents (list[str] | None | Unset): Only these documents of the one project named, by their file below it,
                each a texture set of its own
                whatever it is: a graph in a texturing project or one that is not a material exports too. Null or empty for
                what the project exports.
            presets (list[str] | None | Unset): The presets to export through. Null means the library's `exportTargets`;
                empty, no
                export.
            folder (None | str | Unset): Where exports go, or null for `exports/` in the library.
            options (ExportOptions | None | Unset):
     """

    asset: None | Unset | UUID = UNSET
    projects: list[UUID] | None | Unset = UNSET
    documents: list[str] | None | Unset = UNSET
    presets: list[str] | None | Unset = UNSET
    folder: None | str | Unset = UNSET
    options: ExportOptions | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.export_options import ExportOptions # noqa: PLC0415
        asset: None | str | Unset
        if isinstance(self.asset, Unset):
            asset = UNSET
        elif isinstance(self.asset, UUID):
            asset = str(self.asset)
        else:
            asset = self.asset

        projects: list[str] | None | Unset
        if isinstance(self.projects, Unset):
            projects = UNSET
        elif isinstance(self.projects, list):
            projects = []
            for projects_type_1_item_data in self.projects:
                projects_type_1_item = str(projects_type_1_item_data)
                projects.append(projects_type_1_item)


        else:
            projects = self.projects

        documents: list[str] | None | Unset
        if isinstance(self.documents, Unset):
            documents = UNSET
        elif isinstance(self.documents, list):
            documents = self.documents


        else:
            documents = self.documents

        presets: list[str] | None | Unset
        if isinstance(self.presets, Unset):
            presets = UNSET
        elif isinstance(self.presets, list):
            presets = self.presets


        else:
            presets = self.presets

        folder: None | str | Unset
        if isinstance(self.folder, Unset):
            folder = UNSET
        else:
            folder = self.folder

        options: dict[str, Any] | None | Unset
        if isinstance(self.options, Unset):
            options = UNSET
        elif isinstance(self.options, ExportOptions):
            options = self.options.to_dict()
        else:
            options = self.options


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
        })
        if asset is not UNSET:
            field_dict["asset"] = asset
        if projects is not UNSET:
            field_dict["projects"] = projects
        if documents is not UNSET:
            field_dict["documents"] = documents
        if presets is not UNSET:
            field_dict["presets"] = presets
        if folder is not UNSET:
            field_dict["folder"] = folder
        if options is not UNSET:
            field_dict["options"] = options

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.export_options import ExportOptions # noqa: PLC0415
        d = dict(src_dict)
        def _parse_asset(data: object) -> None | Unset | UUID:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, str):
                    raise TypeError()
                asset_type_1 = UUID(data)



                return asset_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | UUID, data)

        asset = _parse_asset(d.pop("asset", UNSET))


        def _parse_projects(data: object) -> list[UUID] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                projects_type_1 = []
                _projects_type_1 = data
                for projects_type_1_item_data in (_projects_type_1):
                    projects_type_1_item = UUID(projects_type_1_item_data)



                    projects_type_1.append(projects_type_1_item)

                return projects_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[UUID] | None | Unset, data)

        projects = _parse_projects(d.pop("projects", UNSET))


        def _parse_documents(data: object) -> list[str] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                documents_type_1 = cast(list[str], data)

                return documents_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[str] | None | Unset, data)

        documents = _parse_documents(d.pop("documents", UNSET))


        def _parse_presets(data: object) -> list[str] | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, list):
                    raise TypeError()
                presets_type_1 = cast(list[str], data)

                return presets_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(list[str] | None | Unset, data)

        presets = _parse_presets(d.pop("presets", UNSET))


        def _parse_folder(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        folder = _parse_folder(d.pop("folder", UNSET))


        def _parse_options(data: object) -> ExportOptions | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                options_type_1 = ExportOptions.from_dict(data)



                return options_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(ExportOptions | None | Unset, data)

        options = _parse_options(d.pop("options", UNSET))


        build_request = cls(
            asset=asset,
            projects=projects,
            documents=documents,
            presets=presets,
            folder=folder,
            options=options,
        )


        build_request.additional_properties = d
        return build_request

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
