from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from typing import cast

if TYPE_CHECKING:
  from ..models.bake_request import BakeRequest





T = TypeVar("T", bound="ProjectBakePlan")



@_attrs_define
class ProjectBakePlan:
    """ The bakes a project's own settings ask for (#186): one request per texture set to bake, or one for the whole
    mesh, as the Bake dialog sends them. `GET /api/v1/library/project/bake/plan` answers it, and
    `patina bake-maps --project … --dry-run` prints it, so a script bakes a project the way the editor does
    without working the requests out itself.

        Attributes:
            project (str): The project's folder.
            requests (list[BakeRequest]): What to send to `POST /api/v1/jobs/bakes`, one at a time or all at once.
     """

    project: str
    requests: list[BakeRequest]
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.bake_request import BakeRequest # noqa: PLC0415
        project = self.project

        requests = []
        for requests_item_data in self.requests:
            requests_item = requests_item_data.to_dict()
            requests.append(requests_item)




        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "project": project,
            "requests": requests,
        })

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.bake_request import BakeRequest # noqa: PLC0415
        d = dict(src_dict)
        project = d.pop("project")

        requests = []
        _requests = d.pop("requests")
        for requests_item_data in (_requests):
            requests_item = BakeRequest.from_dict(requests_item_data)



            requests.append(requests_item)


        project_bake_plan = cls(
            project=project,
            requests=requests,
        )


        project_bake_plan.additional_properties = d
        return project_bake_plan

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
