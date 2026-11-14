from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.project_bake_plan import ProjectBakePlan
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    *,
    project: str,
    document: str | Unset = UNSET,
    output: str | Unset = UNSET,
    set_: list[str] | Unset = UNSET,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["project"] = project

    params["document"] = document

    params["output"] = output

    json_set_: list[str] | Unset = UNSET
    if not isinstance(set_, Unset):
        json_set_ = set_


    params["set"] = json_set_


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/library/project/bake/plan",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | ProjectBakePlan | None:
    if response.status_code == 200:
        response_200 = ProjectBakePlan.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | ProjectBakePlan]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    document: str | Unset = UNSET,
    output: str | Unset = UNSET,
    set_: list[str] | Unset = UNSET,

) -> Response[ProblemDetails | ProjectBakePlan]:
    """ The bakes a project's own settings ask for, one per texture set to bake or one for its mesh, ready
    for POST /jobs/bakes. A document, by its path below the library, chooses the mesh; output puts the
    maps in a folder of the Server's machine instead of the project's; set, once per texture set, bakes
    those instead of the ones the project ticks.

    Args:
        project (str):
        document (str | Unset):
        output (str | Unset):
        set_ (list[str] | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectBakePlan]
     """


    kwargs = _get_kwargs(
        project=project,
document=document,
output=output,
set_=set_,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    document: str | Unset = UNSET,
    output: str | Unset = UNSET,
    set_: list[str] | Unset = UNSET,

) -> ProblemDetails | ProjectBakePlan | None:
    """ The bakes a project's own settings ask for, one per texture set to bake or one for its mesh, ready
    for POST /jobs/bakes. A document, by its path below the library, chooses the mesh; output puts the
    maps in a folder of the Server's machine instead of the project's; set, once per texture set, bakes
    those instead of the ones the project ticks.

    Args:
        project (str):
        document (str | Unset):
        output (str | Unset):
        set_ (list[str] | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectBakePlan
     """


    return sync_detailed(
        client=client,
project=project,
document=document,
output=output,
set_=set_,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    document: str | Unset = UNSET,
    output: str | Unset = UNSET,
    set_: list[str] | Unset = UNSET,

) -> Response[ProblemDetails | ProjectBakePlan]:
    """ The bakes a project's own settings ask for, one per texture set to bake or one for its mesh, ready
    for POST /jobs/bakes. A document, by its path below the library, chooses the mesh; output puts the
    maps in a folder of the Server's machine instead of the project's; set, once per texture set, bakes
    those instead of the ones the project ticks.

    Args:
        project (str):
        document (str | Unset):
        output (str | Unset):
        set_ (list[str] | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectBakePlan]
     """


    kwargs = _get_kwargs(
        project=project,
document=document,
output=output,
set_=set_,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    document: str | Unset = UNSET,
    output: str | Unset = UNSET,
    set_: list[str] | Unset = UNSET,

) -> ProblemDetails | ProjectBakePlan | None:
    """ The bakes a project's own settings ask for, one per texture set to bake or one for its mesh, ready
    for POST /jobs/bakes. A document, by its path below the library, chooses the mesh; output puts the
    maps in a folder of the Server's machine instead of the project's; set, once per texture set, bakes
    those instead of the ones the project ticks.

    Args:
        project (str):
        document (str | Unset):
        output (str | Unset):
        set_ (list[str] | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectBakePlan
     """


    return (await asyncio_detailed(
        client=client,
project=project,
document=document,
output=output,
set_=set_,

    )).parsed
