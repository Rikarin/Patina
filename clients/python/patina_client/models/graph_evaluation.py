from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_diagnostic import GraphDiagnostic
  from ..models.graph_node_time import GraphNodeTime
  from ..models.graph_preview import GraphPreview





T = TypeVar("T", bound="GraphEvaluation")



@_attrs_define
class GraphEvaluation:
    """ What one evaluation did.

        Attributes:
            revision (int): The document revision it shows; during a drag this trails the newest.
            compiled (bool): True if the plan was compiled; false if the numbers were written into the last one.
            ran (int): How many operations ran. The rest came from the result cache.
            reused (int): How many operations already had a result.
            milliseconds (float): How long the Worker took, previews included.
            previews (list[GraphPreview]): The previews that differ from the last evaluation's. All of them are in the
                state.
            diagnostics (list[GraphDiagnostic]): Everything the compiler reported.
            times (list[GraphNodeTime]): How long each node's operations took on the GPU when they ran. One answered from
                the result cache has the time
                its result took when it was made, for a graph or a thumbnail, and is left out when that was never timed.
            error (None | str | Unset): Why it did not finish, or null. "No Vulkan" means the Worker has no GPU.
     """

    revision: int
    compiled: bool
    ran: int
    reused: int
    milliseconds: float
    previews: list[GraphPreview]
    diagnostics: list[GraphDiagnostic]
    times: list[GraphNodeTime]
    error: None | str | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_diagnostic import GraphDiagnostic # noqa: PLC0415
        from ..models.graph_node_time import GraphNodeTime # noqa: PLC0415
        from ..models.graph_preview import GraphPreview # noqa: PLC0415
        revision = self.revision

        compiled = self.compiled

        ran = self.ran

        reused = self.reused

        milliseconds = self.milliseconds

        previews = []
        for previews_item_data in self.previews:
            previews_item = previews_item_data.to_dict()
            previews.append(previews_item)



        diagnostics = []
        for diagnostics_item_data in self.diagnostics:
            diagnostics_item = diagnostics_item_data.to_dict()
            diagnostics.append(diagnostics_item)



        times = []
        for times_item_data in self.times:
            times_item = times_item_data.to_dict()
            times.append(times_item)



        error: None | str | Unset
        if isinstance(self.error, Unset):
            error = UNSET
        else:
            error = self.error


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "revision": revision,
            "compiled": compiled,
            "ran": ran,
            "reused": reused,
            "milliseconds": milliseconds,
            "previews": previews,
            "diagnostics": diagnostics,
            "times": times,
        })
        if error is not UNSET:
            field_dict["error"] = error

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_diagnostic import GraphDiagnostic # noqa: PLC0415
        from ..models.graph_node_time import GraphNodeTime # noqa: PLC0415
        from ..models.graph_preview import GraphPreview # noqa: PLC0415
        d = dict(src_dict)
        revision = d.pop("revision")

        compiled = d.pop("compiled")

        ran = d.pop("ran")

        reused = d.pop("reused")

        milliseconds = d.pop("milliseconds")

        previews = []
        _previews = d.pop("previews")
        for previews_item_data in (_previews):
            previews_item = GraphPreview.from_dict(previews_item_data)



            previews.append(previews_item)


        diagnostics = []
        _diagnostics = d.pop("diagnostics")
        for diagnostics_item_data in (_diagnostics):
            diagnostics_item = GraphDiagnostic.from_dict(diagnostics_item_data)



            diagnostics.append(diagnostics_item)


        times = []
        _times = d.pop("times")
        for times_item_data in (_times):
            times_item = GraphNodeTime.from_dict(times_item_data)



            times.append(times_item)


        def _parse_error(data: object) -> None | str | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            return cast(None | str | Unset, data)

        error = _parse_error(d.pop("error", UNSET))


        graph_evaluation = cls(
            revision=revision,
            compiled=compiled,
            ran=ran,
            reused=reused,
            milliseconds=milliseconds,
            previews=previews,
            diagnostics=diagnostics,
            times=times,
            error=error,
        )


        graph_evaluation.additional_properties = d
        return graph_evaluation

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
