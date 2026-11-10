from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.project_settings_request_reassign_type_1 import ProjectSettingsRequestReassignType1
  from ..models.unwrap_options import UnwrapOptions





T = TypeVar("T", bound="ProjectSettingsRequest")



@_attrs_define
class ProjectSettingsRequest:
    """ The body of `PATCH /api/v1/library/project/settings`. Null leaves a setting as it is.

        Attributes:
            project (str): The project, below `projects/`.
            mesh (None | str | Unset): A file the Server can read: the project's mesh from now on, under all its texturing
                documents.
            high (None | str | Unset): A file the Server can read: the detailed mesh it bakes from.
            auto_unwrap (bool | None | Unset): Whether a mesh with no UV layout is given one, in a new file beside it. True
                also applies it to the mesh the
                project has now.
            preserve_paint (bool | None | Unset): Whether paint moves to a new mesh's layout, by where it sits on the
                surface. Open documents have to be closed
                first, as it rewrites their paint files.
            unwrap (None | Unset | UnwrapOptions):
            reassign (None | ProjectSettingsRequestReassignType1 | Unset): For a new mesh whose materials are not the old
                one's (#89): which of the new materials each texture set on an
                old material that the new mesh lacks becomes, by name. The set keeps its layers, its paint (moved across with
                Preserve Paint) and its bake folder's name changes with it. A set left out, or sent to a material another set
                holds, stays as it is, and a new material no set takes gets a set of its own.
     """

    project: str
    mesh: None | str | Unset = UNSET
    high: None | str | Unset = UNSET
    auto_unwrap: bool | None | Unset = UNSET
    preserve_paint: bool | None | Unset = UNSET
    unwrap: None | Unset | UnwrapOptions = UNSET
    reassign: None | ProjectSettingsRequestReassignType1 | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.project_settings_request_reassign_type_1 import ProjectSettingsRequestReassignType1 # noqa: PLC0415
        from ..models.unwrap_options import UnwrapOptions # noqa: PLC0415
        project = self.project

        mesh: None | str | Unset
        if isinstance(self.mesh, Unset):
            mesh = UNSET
        else:
            mesh = self.mesh

        high: None | str | Unset
        if isinstance(self.high, Unset):
            high = UNSET
        else:
            high = self.high

        auto_unwrap: bool | None | Unset
        if isinstance(self.auto_unwrap, Unset):
            auto_unwrap = UNSET
        else:
            auto_unwrap = self.auto_unwrap

        preserve_paint: bool | None | Unset
        if isinstance(self.preserve_paint, Unset):
            preserve_paint = UNSET
        else:
            preserve_paint = self.preserve_paint

        unwrap: dict[str, Any] | None | Unset
        if isinstance(self.unwrap, Unset):
            unwrap = UNSET
        elif isinstance(self.unwrap, UnwrapOptions):
            unwrap = self.unwrap.to_dict()
        else:
            unwrap = self.unwrap

        reassign: dict[str, Any] | None | Unset
        if isinstance(self.reassign, Unset):
            reassign = UNSET
        elif isinstance(self.reassign, ProjectSettingsRequestReassignType1):
            reassign = self.reassign.to_dict()
        else:
            reassign = self.reassign


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
        })
        if mesh is not UNSET:
            field_dict["mesh"] = mesh
        if high is not UNSET:
            field_dict["high"] = high
        if auto_unwrap is not UNSET:
            field_dict["autoUnwrap"] = auto_unwrap
        if preserve_paint is not UNSET:
            field_dict["preservePaint"] = preserve_paint
        if unwrap is not UNSET:
            field_dict["unwrap"] = unwrap
        if reassign is not UNSET:
            field_dict["reassign"] = reassign

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.project_settings_request_reassign_type_1 import ProjectSettingsRequestReassignType1 # noqa: PLC0415
        from ..models.unwrap_options import UnwrapOptions # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        def _parse_mesh(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        mesh = _parse_mesh(d.pop("mesh", UNSET))


        def _parse_high(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        high = _parse_high(d.pop("high", UNSET))


        def _parse_auto_unwrap(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        auto_unwrap = _parse_auto_unwrap(d.pop("autoUnwrap", UNSET))


        def _parse_preserve_paint(data: object) -> bool | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(bool | None | Unset, data)

        preserve_paint = _parse_preserve_paint(d.pop("preservePaint", UNSET))


        def _parse_unwrap(data: object) -> None | Unset | UnwrapOptions:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                unwrap_type_1 = UnwrapOptions.from_dict(data)



                return unwrap_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | Unset | UnwrapOptions, data)

        unwrap = _parse_unwrap(d.pop("unwrap", UNSET))


        def _parse_reassign(data: object) -> None | ProjectSettingsRequestReassignType1 | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                reassign_type_1 = ProjectSettingsRequestReassignType1.from_dict(data)



                return reassign_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(None | ProjectSettingsRequestReassignType1 | Unset, data)

        reassign = _parse_reassign(d.pop("reassign", UNSET))


        project_settings_request = cls(
            project=project,
            mesh=mesh,
            high=high,
            auto_unwrap=auto_unwrap,
            preserve_paint=preserve_paint,
            unwrap=unwrap,
            reassign=reassign,
        )


        project_settings_request.additional_properties = d
        return project_settings_request

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
